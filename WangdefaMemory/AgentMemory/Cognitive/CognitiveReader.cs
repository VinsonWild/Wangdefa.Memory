using System.Text.Json;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Signal;

namespace Wangdefa.AgentMemory.Cognitive;

/// <summary>
/// 认知层读取器 - 通过特征推演检索认知卡片
/// </summary>
public class CognitiveReader
{
    private readonly string _recordsPath;
    private readonly FeatureEngine.FeatureEngine _featureEngine;
    private readonly IThinkingStore _thinkingStore;
    private readonly IKnowledgeStore _knowledgeStore;

    // 时间衰减系数，可调
    private const double DECAY_RATE = 0.05;

    public CognitiveReader(
        string recordsPath,
        FeatureEngine.FeatureEngine featureEngine,
        IThinkingStore thinkingStore,
        IKnowledgeStore knowledgeStore)
    {
        _recordsPath = recordsPath;
        _featureEngine = featureEngine;
        _thinkingStore = thinkingStore;
        _knowledgeStore = knowledgeStore;
    }

    public async Task<CognitiveMatchResultModel?> Match(string input, List<string>? history = null, string? topicId = null)
    {
        var results = await MatchTopN(input, history ?? new List<string>(), topicId, 1);
        return results.FirstOrDefault();
    }

    public async Task<CognitiveMatchResultModel?> Match(string input, string[] semanticTags, List<string>? history = null, string? topicId = null)
    {
        var results = await MatchTopN(input, semanticTags, history ?? new List<string>(), topicId, 1);
        return results.FirstOrDefault();
    }

    public async Task<CognitiveMatchResultModel?> MatchByCodes(List<string> codes, string? topicId = null)
    {
        var results = await MatchTopNByCodes(codes, topicId, 1);
        return results.FirstOrDefault();
    }

    public async Task<List<CognitiveMatchResultModel>> MatchTopN(
        string input,
        List<string> history,
        string? topicId = null,
        int topN = 3)
    {
        return await MatchTopN(input, null, history, topicId, topN);
    }

    public async Task<List<CognitiveMatchResultModel>> MatchTopN(
        string input,
        string[]? semanticTags,
        List<string> history,
        string? topicId = null,
        int topN = 3)
    {
        List<string> searchCodes;
        if (semanticTags != null && semanticTags.Length > 0)
        {
            searchCodes = new List<string>();
            foreach (var tag in semanticTags)
            {
                var code = _featureEngine.Tags.GetCode(tag);
                if (code != null)
                {
                    searchCodes.Add(code);
                }
                else
                {
                    var entry = _featureEngine.Tags.Add(tag, "content", "", "auto");
                    searchCodes.Add(entry.Code);
                }
            }
            Console.WriteLine($"🧠 特征推演使用语义标签: {string.Join(", ", semanticTags)} → codes: {string.Join(", ", searchCodes)}");
        }
        else
        {
            searchCodes = _featureEngine.ExtractCodes(input);
            Console.WriteLine($"🧠 特征推演使用原始输入: {input} → codes: {string.Join(", ", searchCodes)}");
        }

        if (searchCodes.Count == 0)
        {
            Console.WriteLine("🧠 未提取到任何检索 code");
            return new List<CognitiveMatchResultModel>();
        }

        return await MatchTopNByCodes(searchCodes, topicId, topN);
    }

    public async Task<List<CognitiveMatchResultModel>> MatchTopNByCodes(
        List<string> codes,
        string? topicId = null,
        int topN = 3)
    {
        if (codes == null || codes.Count == 0)
        {
            Console.WriteLine("🧠 code 列表为空，无法检索");
            return new List<CognitiveMatchResultModel>();
        }

        var featureResults = _featureEngine.Search(
            initialCodes: codes,
            maxDepth: 3,
            maxCards: 50,
            topN: topN * 2);

        if (featureResults == null || featureResults.Count == 0)
        {
            Console.WriteLine($"🧠 特征推演未命中认知卡片 (codes: {string.Join(", ", codes)})");
            return new List<CognitiveMatchResultModel>();
        }

        // ★ 批量并行加载卡片，减少 IO 等待
        var loadTasks = featureResults.Select(fr => LoadCognitiveRecord(fr.CardId));
        var loadedRecords = await Task.WhenAll(loadTasks);

        var results = new List<CognitiveMatchResultModel>();
        var now = DateTime.Now;

        for (int i = 0; i < featureResults.Count; i++)
        {
            var fr = featureResults[i];
            var record = loadedRecords[i];
            if (record == null) continue;

            // ★ 过滤：只返回已补全的卡片，跳过 pending 空卡
            if (record.Status != "completed") continue;

            PerceptionModel? perception = null;
            if (!string.IsNullOrEmpty(record.RecordId))
            {
                var eventModel = await _thinkingStore.LoadEvent(record.RecordId);
                perception = eventModel?.Perception;
            }

            DiversionIndexModel? diversionIndex = null;
            if (!string.IsNullOrEmpty(record.RecordId))
            {
                diversionIndex = await _thinkingStore.LoadIndex(record.RecordId, topicId);
            }

            var createdAt = record.CreatedAt;
            double timeDecay = 1.0;
            if (createdAt != default)
            {
                var daysAgo = (now - createdAt).TotalDays;
                timeDecay = Math.Exp(-DECAY_RATE * daysAgo);
            }

            var finalConfidence = fr.Strength * timeDecay;

            results.Add(new CognitiveMatchResultModel
            {
                Summary = record.Insight?.Summary ?? "",
                ContentTags = record.Insight?.ContentTags ?? new string[0],
                RelationTags = record.Insight?.RelationTags ?? new List<RelationTag>(),
                RecordId = record.RecordId,
                Perception = perception ?? record.Perception,
                Insight = record.Insight,
                Confidence = finalConfidence,
                CreatedAt = createdAt,
                SummaryPointer = diversionIndex?.SummaryPointer,
                OverviewPointer = diversionIndex?.OverviewPointer,
                FullTextPointer = diversionIndex?.FullTextPointer,
                FullTextType = diversionIndex?.FullTextType,
                SourcePath = record.SourcePath,
                Preferences = record.Insight?.Preferences ?? new List<PreferenceEntry>()
            });
        }

        results = results
            .OrderByDescending(r => r.Confidence)
            .Take(topN)
            .ToList();

        Console.WriteLine($"🧠 认知层命中 {results.Count} 条记录（仅 completed，时间衰减已应用）");
        return results;
    }

    private async Task<CognitiveRecordModel?> LoadCognitiveRecord(string recordId)
    {
        var path = Path.Combine(_recordsPath, $"{recordId}.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<CognitiveRecordModel>(json);
    }
}
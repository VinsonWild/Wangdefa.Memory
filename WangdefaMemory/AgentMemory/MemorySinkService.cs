// ================================================================
// MemorySinkService.cs — 记忆体写入（含偏好存储）
// ================================================================

using System.Text.Json;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Knowledge;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Signal;
using Wangdefa.AgentMemory.Thinking;
using Wangdefa.AgentMemory.Thinking.Events;
using Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;

namespace Wangdefa.AgentMemory;

public class MemorySinkService : IMemorySinkService
{
    private readonly string _recordsPath;
    private readonly string _knowledgePath;
    private readonly FeatureEngine.FeatureEngine _featureEngine;
    private readonly IThinkingStore _thinkingStore;
    private readonly IKnowledgeStore _knowledgeStore;
    private readonly IEventStore _eventStore;
    private readonly ILearningOrchestrator _learningOrchestrator;
    private readonly ISQLiteTools _sqliteTools; // ✅ 依赖接口，不依赖具体实现

    public MemorySinkService(
        string recordsPath,
        string basePath,
        FeatureEngine.FeatureEngine featureEngine,
        IThinkingStore thinkingStore,
        IKnowledgeStore knowledgeStore,
        IEventStore eventStore,
        ILearningOrchestrator learningOrchestrator,
        ISQLiteTools sqliteTools) // ✅ 构造函数接收接口
    {
        _recordsPath = recordsPath;
        _knowledgePath = Path.Combine(basePath, "experience", "knowledge");
        _featureEngine = featureEngine;
        _thinkingStore = thinkingStore;
        _knowledgeStore = knowledgeStore;
        _eventStore = eventStore;
        _learningOrchestrator = learningOrchestrator;
        _sqliteTools = sqliteTools;
    }

    public async Task SinkAsync(
        string userInput,
        string agentResponse,
        string topicId,
        PerceptionModel perception,
        string summary,
        string overview,
        List<string> tags,
        string route,
        string? sourcePath = null,
        string? sourceType = null,
        Dictionary<string, string>? missingTagDefinitions = null,
        List<PreferenceEntry>? preferences = null)
    {
        try
        {
            var recordId = $"认知_{DateTime.Now:yyyyMMdd_HHmmss}";
            var thinkingRecordId = $"记录_{DateTime.Now:yyyyMMdd_HHmmss}";

            // ===== 1. 构建认知记录（含偏好） =====
            var insight = new InsightModel
            {
                ContentTags = tags.ToArray(),
                RelationTags = new List<RelationTag>(),
                Summary = summary,
                Preferences = preferences ?? new List<PreferenceEntry>()
            };

            // ===== 反馈评价（已移至 C线 LLM 判断） =====
            var previousRecordId = await _thinkingStore.GetLatestRecordId(topicId);
            if (!string.IsNullOrEmpty(previousRecordId))
            {
                var previous = await _thinkingStore.LoadChatRecord(previousRecordId, topicId);
                if (previous != null && previous.UserFeedback.Status == "pending")
                {
                    // 保持 pending，等待 C线 的 SummaryAnalyzer 通过 LLM 判断后写入
                }
            }

            var cognitiveRecord = new CognitiveRecordModel
            {
                Id = recordId,
                Perception = perception,
                Insight = insight,
                RecordId = thinkingRecordId,
                CreatedAt = DateTime.Now,
                Weight = 1.0,
                LastAccessAt = DateTime.Now,
                SourcePath = sourcePath ?? ""
            };

            var cognitivePath = Path.Combine(_recordsPath, $"{recordId}.json");
            var cognitiveJson = JsonSerializer.Serialize(cognitiveRecord, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cognitivePath, cognitiveJson);

            // ===== 2. 特征推演更新（打标签）- 统一由 TagCard 处理 =====
            if (tags.Count > 0)
            {
                _featureEngine.TagCard(recordId, tags.ToList(), "cognitive", missingTagDefinitions);
                Console.WriteLine($"✅ 特征推演已更新: {recordId}");
            }

            // ===== 3. 分流索引 =====
            var diversionIndex = new DiversionIndexModel
            {
                CognitiveRecordId = recordId,
                EventType = string.IsNullOrEmpty(sourcePath) ? "chat" : "file",
                TopicId = topicId,
                SummaryPointer = $"knowledge/{topicId}/摘要_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                OverviewPointer = $"knowledge/{topicId}/概览_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                FullTextPointer = sourcePath ?? "",
                FullTextType = string.IsNullOrEmpty(sourcePath) ? "db" : "file",
                CreatedAt = DateTime.Now,
                LastAccessAt = DateTime.Now
            };

            await _thinkingStore.SaveIndex(diversionIndex, topicId);

            cognitiveRecord.RecordId = thinkingRecordId;
            var updatedJson = JsonSerializer.Serialize(cognitiveRecord, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cognitivePath, updatedJson);

            // ===== 4. 写入知识层（概览 + 摘要） =====
            if (!string.IsNullOrEmpty(overview))
            {
                var overviewModel = new OverviewModel
                {
                    Id = $"概览_{DateTime.Now:yyyyMMdd_HHmmss}",
                    TopicId = topicId,
                    CognitiveRecordId = recordId,
                    Text = overview,
                    ContentType = "chat",
                    WordCount = overview.Length,
                    Confidence = 0.8,
                    CreatedAt = DateTime.Now
                };

                var overviewPath = Path.Combine(_knowledgePath, topicId, $"{overviewModel.Id}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(overviewPath)!);
                await File.WriteAllTextAsync(overviewPath, JsonSerializer.Serialize(overviewModel, new JsonSerializerOptions { WriteIndented = true }));
            }

            // ===== 5. SQLite 备份 =====
            var perceptionJson = JsonSerializer.Serialize(perception);
            // 通过接口调用，不直接依赖 SQLiteTools 类
            var writeResult = await _sqliteTools.WriteRecord(
                userInput,
                agentResponse,
                topicId,
                string.Join(",", tags),
                summary,
                0.8,
                perceptionJson,
                route,
                overview
            );
            Console.WriteLine(writeResult);

            // ===== 6. 事件存储 =====
            var evt = new EventModel
            {
                EventId = $"事件_{DateTime.Now:yyyyMMdd_HHmmss}",
                EventType = string.IsNullOrEmpty(sourcePath) ? "chat" : "file",
                EventLevel = "point",
                Mode = "wangdefa_full",
                TopicId = topicId,
                Timestamp = DateTime.Now,
                Perception = perception,
                Data = new EventData
                {
                    UserInput = userInput,
                    AgentResponse = agentResponse,
                    FilePath = sourcePath,
                    FileName = !string.IsNullOrEmpty(sourcePath) ? Path.GetFileName(sourcePath) : null,
                    FileAction = sourceType == "file" ? "upload" : null
                },
                Context = new EventContext
                {
                    Source = "agent"
                },
                Result = new EventResult
                {
                    Status = "completed",
                    Summary = summary,
                    Route = route,
                    DurationMs = null
                },
                CognitiveRecordId = recordId,
                FeatureTags = tags.ToArray(),
            };

            await _eventStore.SaveAsync(evt);
            Console.WriteLine($"✅ 事件已存储: {evt.EventId}");

            // ===== 7. 后台学习 =====
            _ = Task.Run(async () =>
            {
                try
                {
                    await TriggerLearningAsync(evt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 思考层学习失败: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ SinkAsync 写入失败: {ex.Message}");
        }
    }

    private async Task TriggerLearningAsync(EventModel evt)
    {
        await _learningOrchestrator.ProcessAsync(evt);

        if (!string.IsNullOrEmpty(evt.Data.FilePath) && File.Exists(evt.Data.FilePath))
        {
            await GenerateFileSummaryAndOverview(evt.Data.FilePath, evt.TopicId);
        }

        await _knowledgeStore.RebuildIndex(evt.TopicId);
    }

    private async Task GenerateFileSummaryAndOverview(string filePath, string topicId)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrEmpty(content)) return;

            var keywords = ExtractKeywordsFromText(content);

            var summary = new SummaryModel
            {
                Id = $"概要_{DateTime.Now:yyyyMMdd_HHmmss}",
                TopicId = topicId,
                CognitiveRecordId = "",
                Keywords = keywords,
                Entities = Array.Empty<string>(),
                DateRange = "",
                Summary = content.Length > 200 ? content.Substring(0, 200) : content,
                Confidence = 0.8,
                CreatedAt = DateTime.Now,
                ModifiedAt = File.GetLastWriteTimeUtc(filePath)
            };

            var overview = new OverviewModel
            {
                Id = $"概览_{DateTime.Now:yyyyMMdd_HHmmss}",
                TopicId = topicId,
                CognitiveRecordId = "",
                Text = content.Length > 500 ? content.Substring(0, 500) + "..." : content,
                ContentType = "document",
                WordCount = content.Length,
                Confidence = 0.8,
                CreatedAt = DateTime.Now
            };

            var summaryPath = Path.Combine(_knowledgePath, topicId, $"{summary.Id}.json");
            var overviewPath = Path.Combine(_knowledgePath, topicId, $"{overview.Id}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);

            await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(overviewPath, JsonSerializer.Serialize(overview, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[学习层] 生成概要/概览失败: {ex.Message}");
        }
    }

    private string[] ExtractKeywordsFromText(string text)
    {
        var words = text.Split(new[] { ' ', '\n', '\r', '，', '。', '、', '！', '？', ',', '.', '!' }, StringSplitOptions.RemoveEmptyEntries);
        var freq = words.GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());
        return freq.OrderByDescending(kv => kv.Value).Take(10).Select(kv => kv.Key).ToArray();
    }
}
// ================================================================
// MemorySinkService.cs — 记忆体写入（含偏好存储）
// 修复指针一致性问题 v2
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
using Wangdefa.Contracts;

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
    private readonly ISQLiteTools _sqliteTools;
    private readonly IChatService _chatService;

    public MemorySinkService(
        string recordsPath,
        string basePath,
        FeatureEngine.FeatureEngine featureEngine,
        IThinkingStore thinkingStore,
        IKnowledgeStore knowledgeStore,
        IEventStore eventStore,
        ILearningOrchestrator learningOrchestrator,
        ISQLiteTools sqliteTools,
        IChatService chatService)
    {
        _recordsPath = recordsPath;
        _knowledgePath = Path.Combine(basePath, "experience", "knowledge");
        _featureEngine = featureEngine;
        _thinkingStore = thinkingStore;
        _knowledgeStore = knowledgeStore;
        _eventStore = eventStore;
        _learningOrchestrator = learningOrchestrator;
        _sqliteTools = sqliteTools;
        _chatService = chatService;
    }

    // ============================================================
    // 原有 SinkAsync（完整写入，保留兼容）
    // ============================================================
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
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var recordId = $"认知_{timestamp}";
            var thinkingRecordId = $"记录_{timestamp}";
            var eventId = $"事件_{timestamp}";

            var insight = new InsightModel
            {
                ContentTags = tags.ToArray(),
                RelationTags = new List<RelationTag>(),
                Summary = summary,
                Preferences = preferences ?? new List<PreferenceEntry>()
            };

            var cognitiveRecord = new CognitiveRecordModel
            {
                Id = recordId,
                Perception = perception,
                Insight = insight,
                RecordId = thinkingRecordId,
                EventId = eventId,
                CreatedAt = DateTime.Now,
                Weight = 1.0,
                LastAccessAt = DateTime.Now,
                SourcePath = sourcePath ?? "",
                Status = "completed",
                TopicId = topicId
            };

            var cognitivePath = Path.Combine(_recordsPath, $"{recordId}.json");
            var cognitiveJson = JsonSerializer.Serialize(cognitiveRecord, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cognitivePath, cognitiveJson);

            if (tags.Count > 0)
            {
                _featureEngine.TagCard(recordId, tags.ToList(), "cognitive", missingTagDefinitions);
                Console.WriteLine($"✅ 特征推演已更新: {recordId}");
            }

            var diversionIndex = new DiversionIndexModel
            {
                CognitiveRecordId = recordId,
                EventType = string.IsNullOrEmpty(sourcePath) ? "chat" : "file",
                TopicId = topicId,
                SummaryPointer = $"knowledge/{topicId}/摘要_{timestamp}.json",
                OverviewPointer = $"knowledge/{topicId}/概览_{timestamp}.json",
                FullTextPointer = sourcePath ?? "",
                FullTextType = string.IsNullOrEmpty(sourcePath) ? "db" : "file",
                CreatedAt = DateTime.Now,
                LastAccessAt = DateTime.Now
            };

            await SaveIndexWithIdAsync(thinkingRecordId, diversionIndex, topicId);

            cognitiveRecord.RecordId = thinkingRecordId;
            var updatedJson = JsonSerializer.Serialize(cognitiveRecord, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cognitivePath, updatedJson);

            if (!string.IsNullOrEmpty(overview))
            {
                var overviewModel = new OverviewModel
                {
                    Id = $"概览_{timestamp}",
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

            var perceptionJson = JsonSerializer.Serialize(perception);
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

            var evt = new EventModel
            {
                EventId = eventId,
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
            throw;
        }
    }

    // ============================================================
    // ★ 修复：前置写入卡片框架（统一ID）
    // ============================================================
    public async Task<string> WriteFrameAsync(
        string topicId,
        string userInput,
        PerceptionModel perception,
        List<string> tags,
        string route,
        string? sourcePath = null,
        string? sourceType = null)
    {
        // ★★★ 统一时间戳，三处共用 ★★★
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var cardId = $"认知_{timestamp}";
        var indexId = $"记录_{timestamp}";
        var eventId = $"事件_{timestamp}";

        var insight = new InsightModel
        {
            ContentTags = tags.ToArray(),
            RelationTags = new List<RelationTag>(),
            Summary = "",
            Preferences = new List<PreferenceEntry>()
        };

        var cognitiveRecord = new CognitiveRecordModel
        {
            Id = cardId,
            Perception = perception,
            Insight = insight,
            RecordId = indexId,
            EventId = eventId,
            CreatedAt = DateTime.Now,
            Weight = 1.0,
            LastAccessAt = DateTime.Now,
            SourcePath = sourcePath ?? "",
            Status = "pending",
            TopicId = topicId
        };

        // 1. 保存认知卡片
        var cognitivePath = Path.Combine(_recordsPath, $"{cardId}.json");
        var cognitiveJson = JsonSerializer.Serialize(cognitiveRecord, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(cognitivePath, cognitiveJson);

        // 2. 打标签
        if (tags.Count > 0)
        {
            _featureEngine.TagCard(cardId, tags.ToList(), "cognitive", null);
            Console.WriteLine($"✅ C线框架已写入: {cardId}，状态: pending");
        }

        // 3. ★ 保存索引记录（使用统一 indexId）
        var diversionIndex = new DiversionIndexModel
        {
            CognitiveRecordId = cardId,
            EventType = string.IsNullOrEmpty(sourcePath) ? "chat" : "file",
            TopicId = topicId,
            SummaryPointer = $"knowledge/{topicId}/摘要_{timestamp}.json",
            OverviewPointer = $"knowledge/{topicId}/概览_{timestamp}.json",
            FullTextPointer = sourcePath ?? "",
            FullTextType = string.IsNullOrEmpty(sourcePath) ? "db" : "file",
            CreatedAt = DateTime.Now,
            LastAccessAt = DateTime.Now
        };

        await SaveIndexWithIdAsync(indexId, diversionIndex, topicId);

        // 更新认知卡片的 RecordId
        cognitiveRecord.RecordId = indexId;
        var updatedJson = JsonSerializer.Serialize(cognitiveRecord, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(cognitivePath, updatedJson);

        // 4. ★ 保存事件（使用统一 eventId）
        var evt = new EventModel
        {
            EventId = eventId,
            EventType = string.IsNullOrEmpty(sourcePath) ? "chat" : "file",
            EventLevel = "point",
            Mode = "wangdefa_full",
            TopicId = topicId,
            Timestamp = DateTime.Now,
            Perception = perception,
            Data = new EventData
            {
                UserInput = userInput,
                AgentResponse = "",
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
                Status = "pending",
                Summary = "",
                Route = route,
                DurationMs = null
            },
            CognitiveRecordId = cardId,
            FeatureTags = tags.ToArray(),
        };

        await _eventStore.SaveAsync(evt);
        Console.WriteLine($"✅ 事件框架已存储: {evt.EventId}，状态: pending");

        return cardId;
    }

    // ============================================================
    // ★ 修复：补全卡片（修正事件查找）
    // ============================================================
    public async Task CompleteAsync(
        string cardId,
        string userInput,
        string agentResponse,
        string status,
        string? errorMessage = null)
    {
        var cognitivePath = Path.Combine(_recordsPath, $"{cardId}.json");
        if (!File.Exists(cognitivePath))
        {
            throw new FileNotFoundException($"卡片文件不存在: {cognitivePath}");
        }

        var json = await File.ReadAllTextAsync(cognitivePath);
        var cognitiveRecord = JsonSerializer.Deserialize<CognitiveRecordModel>(json);
        if (cognitiveRecord == null)
        {
            throw new InvalidOperationException($"卡片反序列化失败: {cognitivePath}");
        }

        // ★ 从卡片中获取事件ID（写框架时已保存）
        string? eventId = cognitiveRecord.EventId;

        // 如果卡片里没有 EventId（兼容旧数据），从 cardId 推导
        if (string.IsNullOrEmpty(eventId))
        {
            var parts = cardId.Split('_');
            if (parts.Length >= 3)
            {
                var timestamp = string.Join("_", parts.Skip(1));
                eventId = $"事件_{timestamp}";
                Console.WriteLine($"[CompleteAsync] 从 cardId 推导 EventId: {eventId}");
            }
        }

        // ★ 用事件ID加载事件
        EventModel? eventModel = null;
        if (!string.IsNullOrEmpty(eventId))
        {
            eventModel = await _eventStore.LoadAsync(eventId);
            if (eventModel == null)
            {
                Console.WriteLine($"[CompleteAsync] ⚠️ 事件不存在: {eventId}，尝试用 CognitiveRecordId 查找...");
                var todayEvents = await _eventStore.GetDayEventsAsync(DateTime.Now);
                eventModel = todayEvents.FirstOrDefault(e => e.CognitiveRecordId == cardId);
            }
        }

        // 兜底：用 CognitiveRecordId 查
        if (eventModel == null)
        {
            Console.WriteLine($"[CompleteAsync] ⚠️ 仍未找到事件，尝试用 CognitiveRecordId 查询...");
            var todayEvents = await _eventStore.GetDayEventsAsync(DateTime.Now);
            eventModel = todayEvents.FirstOrDefault(e => e.CognitiveRecordId == cardId);
        }

        // ===== C线：摘要生成 =====
        var contentTags = cognitiveRecord.Insight?.ContentTags ?? Array.Empty<string>();
        var structuredTagsFromCard = contentTags.Select(t => new StructuredTag { Tag = t }).ToArray();

        var summaryAnalyzer = new SummaryAnalyzer(_chatService);
        var summaryResult = await summaryAnalyzer.AnalyzeAsync(
            userInput: userInput,
            agentResponse: agentResponse,
            structuredTags: structuredTagsFromCard,
            missingTags: null
        );

        // 更新摘要
        cognitiveRecord.Insight.Summary = !string.IsNullOrEmpty(summaryResult.Summary)
            ? summaryResult.Summary
            : (!string.IsNullOrEmpty(agentResponse)
                ? (agentResponse.Length > 100 ? agentResponse.Substring(0, 100) : agentResponse)
                : "");

        cognitiveRecord.Status = status;
        cognitiveRecord.Weight = 1.0;
        cognitiveRecord.LastAccessAt = DateTime.Now;

        if (status == "failed" && !string.IsNullOrEmpty(errorMessage))
        {
            cognitiveRecord.Insight.Summary = $"错误: {errorMessage}";
        }

        // 更新缺失标签定义
        if (summaryResult.MissingTagDefinitions != null && summaryResult.MissingTagDefinitions.Count > 0)
        {
            foreach (var kv in summaryResult.MissingTagDefinitions)
            {
                var code = _featureEngine.Tags.GetCode(kv.Key);
                if (code != null)
                {
                    _featureEngine.Tags.UpdateDefinition(code, kv.Value);
                }
            }
            Console.WriteLine($"✅ 已更新 {summaryResult.MissingTagDefinitions.Count} 个缺失标签定义");
        }

        // 更新偏好
        if (summaryResult.Preferences != null && summaryResult.Preferences.Count > 0)
        {
            cognitiveRecord.Insight.Preferences = summaryResult.Preferences;
        }

        // 保存概览
        if (!string.IsNullOrEmpty(summaryResult.Overview))
        {
            var topicId = cognitiveRecord.TopicId ?? "default";
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var overviewModel = new OverviewModel
            {
                Id = $"概览_{timestamp}",
                TopicId = topicId,
                CognitiveRecordId = cardId,
                Text = summaryResult.Overview,
                ContentType = "chat",
                WordCount = summaryResult.Overview.Length,
                Confidence = 0.8,
                CreatedAt = DateTime.Now
            };

            var overviewPath = Path.Combine(_knowledgePath, topicId, $"{overviewModel.Id}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(overviewPath)!);
            await File.WriteAllTextAsync(overviewPath, JsonSerializer.Serialize(overviewModel, new JsonSerializerOptions { WriteIndented = true }));

            cognitiveRecord.SourcePath = Path.Combine(topicId, $"{overviewModel.Id}.json");
            Console.WriteLine($"✅ 概览已保存: {overviewPath}");
        }

        // 保存认知卡片
        var updatedJson = JsonSerializer.Serialize(cognitiveRecord, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(cognitivePath, updatedJson);

        // ★ 更新事件（找到了才更新）
        if (eventModel != null)
        {
            eventModel.Data.AgentResponse = agentResponse;
            eventModel.Result.Status = status;
            eventModel.Result.Summary = summaryResult.Summary ?? "";
            if (status == "failed" && !string.IsNullOrEmpty(errorMessage))
            {
                eventModel.Result.ErrorMessage = errorMessage;
            }
            await _eventStore.SaveAsync(eventModel);
            Console.WriteLine($"✅ 事件已补全: {eventModel.EventId}，状态: {status}");
        }
        else
        {
            Console.WriteLine($"[CompleteAsync] ⚠️ 未能找到关联事件，状态更新可能不完整");
        }

        // 更新特征统计
        if (cognitiveRecord.Insight.ContentTags?.Length > 0)
        {
            var codes = cognitiveRecord.Insight.ContentTags
                .Select(t => _featureEngine.Tags.GetCode(t))
                .Where(c => c != null)
                .Select(c => c!)
                .ToList();
            if (codes.Count > 0)
            {
                _featureEngine.Stats.RecordHit(codes);
                Console.WriteLine($"✅ 标签统计已更新: {string.Join(", ", codes)}");
            }
        }

        // 写入 SQLite
        var perceptionJson = JsonSerializer.Serialize(cognitiveRecord.Perception);
        await _sqliteTools.WriteRecord(
            cognitiveRecord.Insight.Summary ?? "",
            agentResponse,
            cognitiveRecord.TopicId ?? "default",
            string.Join(",", cognitiveRecord.Insight.ContentTags ?? Array.Empty<string>()),
            cognitiveRecord.Insight.Summary ?? "",
            0.8,
            perceptionJson,
            "shallow",
            ""
        );

        Console.WriteLine($"✅ CompleteAsync: 卡片已补全 {cardId}，状态: {status}");
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    /// <summary>
    /// 用指定ID保存索引
    /// </summary>
    private async Task SaveIndexWithIdAsync(string indexId, DiversionIndexModel index, string topicId)
    {
        var chatPath = _thinkingStore.GetTopicPath(topicId);
        var path = Path.Combine(chatPath, $"{indexId}.json");
        var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
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
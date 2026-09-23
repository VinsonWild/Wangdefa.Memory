// ================================================================
// MemorySinkService.cs — 记忆体写入（门面）
// ================================================================

using System.Text.Json;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.FeatureEngine.Models;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Knowledge;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Services;
using Wangdefa.AgentMemory.Signal;
using Wangdefa.AgentMemory.Storage;
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
    private readonly MemoryStore _store;
    private readonly SceneStore _sceneStore;

    private readonly PreferenceService _preferenceService;
    private readonly FeedbackService _feedbackService;
    private readonly TagMergeService _tagMergeService;

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

        _store = new MemoryStore(basePath);
        _sceneStore = new SceneStore(basePath);

        _preferenceService = new PreferenceService();
        _feedbackService = new FeedbackService();
        _tagMergeService = new TagMergeService(_featureEngine, _recordsPath, _store);
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

            await _store.WriteCardAsync(recordId, cognitiveRecord);

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
                EventId = eventId,
                CreatedAt = DateTime.Now,
                LastAccessAt = DateTime.Now
            };

            await _store.WriteIndexAsync(thinkingRecordId, diversionIndex, topicId);

            if (!string.IsNullOrEmpty(overview))
            {
                await _store.WriteOverviewAsync(topicId, recordId, overview);
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

            await _store.WriteEventAsync(evt);
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
    // ★ 前置写入卡片框架（统一ID）
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

        await _store.WriteCardAsync(cardId, cognitiveRecord);

        if (tags.Count > 0)
        {
            _featureEngine.TagCard(cardId, tags.ToList(), "cognitive", null);
            Console.WriteLine($"✅ C线框架已写入: {cardId}，状态: pending");
        }

        var diversionIndex = new DiversionIndexModel
        {
            CognitiveRecordId = cardId,
            EventType = string.IsNullOrEmpty(sourcePath) ? "chat" : "file",
            TopicId = topicId,
            SummaryPointer = $"knowledge/{topicId}/摘要_{timestamp}.json",
            OverviewPointer = $"knowledge/{topicId}/概览_{timestamp}.json",
            FullTextPointer = sourcePath ?? "",
            FullTextType = string.IsNullOrEmpty(sourcePath) ? "db" : "file",
            EventId = eventId,
            CreatedAt = DateTime.Now,
            LastAccessAt = DateTime.Now
        };

        await _store.WriteIndexAsync(indexId, diversionIndex, topicId);

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

        await _store.WriteEventAsync(evt);
        Console.WriteLine($"✅ 事件框架已存储: {evt.EventId}，状态: pending");

        return cardId;
    }

    // ============================================================
    // ★ 补全卡片（门面：调度各 Service）
    // ============================================================
    public async Task CompleteAsync(
        string cardId,
        string userInput,
        string agentResponse,
        string status,
        string? errorMessage = null,
        List<TagEntry>? malformedTags = null)   // ★ 批次 E：可选，有传就用（E-3 口径），没传就兜底重测
    {
        // 1. 加载卡片
        var cognitiveRecord = await _store.ReadCardAsync(cardId);
        if (cognitiveRecord == null)
            throw new FileNotFoundException($"卡片文件不存在: {cardId}");

        // 2. 获取事件
        string? eventId = cognitiveRecord.EventId;
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

        if (eventModel == null)
        {
            Console.WriteLine($"[CompleteAsync] ⚠️ 仍未找到事件，尝试用 CognitiveRecordId 查询...");
            var todayEvents = await _eventStore.GetDayEventsAsync(DateTime.Now);
            eventModel = todayEvents.FirstOrDefault(e => e.CognitiveRecordId == cardId);
        }

        // 3. 准备标签
        var contentTags = cognitiveRecord.Insight?.ContentTags ?? Array.Empty<string>();
        var structuredTagsFromCard = contentTags.Select(t => new StructuredTag { Tag = t }).ToArray();
        var tagsToJudge = _tagMergeService.GetTagsToJudge(cognitiveRecord);

        // ★ 批次 E：残缺标签 —— 有传就用（E-3 口径），没传就兜底重测
        if (malformedTags == null)
        {
            malformedTags = new List<TagEntry>();
            foreach (var tagName in contentTags.Distinct())
            {
                var entry = _featureEngine.Tags.GetEntry(tagName);
                if (entry != null && _featureEngine.Tags.IsMalformed(entry))
                    malformedTags.Add(entry);
            }
            Console.WriteLine($"[CompleteAsync] 兜底检测到 {malformedTags.Count} 个残缺标签");
        }
        else
        {
            Console.WriteLine($"[CompleteAsync] 使用传入的 {malformedTags.Count} 个残缺标签（E-3 口径）");
        }

        // 4. 获取上一轮概览
        var previousAgentResponse = await GetPreviousOverviewAsync(cognitiveRecord.TopicId ?? "default");

        // 5. 调用 SummaryAnalyzer
        var summaryAnalyzer = new SummaryAnalyzer(_chatService);
        var summaryResult = await summaryAnalyzer.AnalyzeAsync(
            userInput: userInput,
            agentResponse: agentResponse,
            previousAgentResponse: previousAgentResponse,
            structuredTags: structuredTagsFromCard,
            missingTags: null,
            pendingTags: tagsToJudge,
            malformedTags: malformedTags   // ★ 批次 E
        );

        // 6. 更新卡片基础信息
        cognitiveRecord.Insight.Summary = !string.IsNullOrEmpty(summaryResult.Summary)
            ? summaryResult.Summary
            : (!string.IsNullOrEmpty(agentResponse)
                ? (agentResponse.Length > 100 ? agentResponse.Substring(0, 100) : agentResponse)
                : "");
        cognitiveRecord.Status = status;
        cognitiveRecord.Weight = 1.0;
        cognitiveRecord.LastAccessAt = DateTime.Now;

        if (status == "failed" && !string.IsNullOrEmpty(errorMessage))
            cognitiveRecord.Insight.Summary = $"错误: {errorMessage}";

        // 7. 更新缺失标签定义
        if (summaryResult.MissingTagDefinitions != null && summaryResult.MissingTagDefinitions.Count > 0)
        {
            foreach (var kv in summaryResult.MissingTagDefinitions)
            {
                var code = _featureEngine.Tags.GetCode(kv.Key);
                if (code != null)
                    _featureEngine.Tags.UpdateDefinition(code, kv.Value);
            }
            Console.WriteLine($"✅ 已更新 {summaryResult.MissingTagDefinitions.Count} 个缺失标签定义");
        }

        // 8. 合并偏好
        var existingPrefs = cognitiveRecord.Insight.Preferences ?? new List<PreferenceEntry>();
        if (summaryResult.Preferences != null && summaryResult.Preferences.Count > 0)
        {
            cognitiveRecord.Insight.Preferences = _preferenceService.Merge(existingPrefs, summaryResult.Preferences);
            Console.WriteLine($"[CompleteAsync] 偏好已合并，共 {cognitiveRecord.Insight.Preferences.Count} 条");
        }
        else
        {
            cognitiveRecord.Insight.Preferences ??= new List<PreferenceEntry>();
            Console.WriteLine($"[CompleteAsync] 本轮无新偏好，保留原有 {cognitiveRecord.Insight.Preferences.Count} 条");
        }

        // 9. 写入 feedback（卡片 + 事件）
        _feedbackService.ApplyToCard(cognitiveRecord, summaryResult.Feedback);
        _feedbackService.SaveToEvent(eventModel, summaryResult.Feedback);

        // 10. 保存概览
        if (!string.IsNullOrEmpty(summaryResult.Overview))
        {
            cognitiveRecord.SourcePath = await _store.WriteOverviewAsync(
                cognitiveRecord.TopicId ?? "default",
                cardId,
                summaryResult.Overview
            );
        }

        // ════════════════════════════════════════════════════════════
        // ★ 11. C线为主、A线兜底：按字段分别取值定稿场景
        // ════════════════════════════════════════════════════════════
        var aCategory = cognitiveRecord.Perception.Scene;      // A线在 pending 卡里的值
        var aSub = cognitiveRecord.Perception.SceneSub;

        var category = !string.IsNullOrEmpty(summaryResult.SceneCategory)
            ? summaryResult.SceneCategory
            : aCategory;

        var sub = !string.IsNullOrEmpty(summaryResult.SceneSub)
            ? summaryResult.SceneSub
            : (!string.IsNullOrEmpty(aSub) ? aSub : "");

        if (!string.IsNullOrEmpty(category))
        {
            // name 拼装：sub 为空时用 NA 占位，避免 code 落成 UNKNOWN
            var name = string.IsNullOrEmpty(sub) ? $"{category}-NA" : $"{category}-{sub}";
            var subDisplay = string.IsNullOrEmpty(sub) ? "NA" : sub;

            // 先查是否存在
            var existing = _sceneStore.Get(category, sub, name);
            if (existing == null)
            {
                _sceneStore.Add(category, sub, name);
                Console.WriteLine($"[CompleteAsync] 场景已收录: {category}/{subDisplay}");
            }
            else
            {
                Console.WriteLine($"[CompleteAsync] 场景已存在: {category}/{subDisplay}");
            }

            // 写回卡片 Perception
            cognitiveRecord.Perception.Scene = category;
            cognitiveRecord.Perception.SceneSub = sub;

            Console.WriteLine($"[CompleteAsync] 场景定稿: C={summaryResult.SceneCategory}/{summaryResult.SceneSub}, A={aCategory}/{aSub}, 最终={category}/{subDisplay}");
        }
        else
        {
            Console.WriteLine($"[CompleteAsync] 无场景信息（C和A均为空），跳过场景处理");
        }

        // 12. 保存卡片
        await _store.WriteCardAsync(cardId, cognitiveRecord);

        // 13. 更新事件
        if (eventModel != null)
        {
            eventModel.Data.AgentResponse = agentResponse;
            eventModel.Result.Status = status;
            eventModel.Result.Summary = summaryResult.Summary ?? "";
            if (status == "failed" && !string.IsNullOrEmpty(errorMessage))
                eventModel.Result.ErrorMessage = errorMessage;
            await _store.WriteEventAsync(eventModel);
            Console.WriteLine($"✅ 事件已补全: {eventModel.EventId}，状态: {status}");
        }
        else
        {
            Console.WriteLine($"[CompleteAsync] ⚠️ 未能找到关联事件，状态更新可能不完整");
        }

        // 14. 更新特征统计
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

        // 15. 写入 SQLite
        var perceptionJson = JsonSerializer.Serialize(cognitiveRecord.Perception);
        var realRoute = eventModel?.Result?.Route ?? "shallow";
        await _sqliteTools.WriteRecord(
            cognitiveRecord.Insight.Summary ?? "",
            agentResponse,
            cognitiveRecord.TopicId ?? "default",
            string.Join(",", cognitiveRecord.Insight.ContentTags ?? Array.Empty<string>()),
            cognitiveRecord.Insight.Summary ?? "",
            0.8,
            perceptionJson,
            realRoute,
            ""
        );

        // 16. 执行标签合并
        if (summaryResult.PendingTagsDecision != null && summaryResult.PendingTagsDecision.Count > 0)
        {
            var allPendingTags = _featureEngine.Tags.GetUnexaminedTagsByNames(
                cognitiveRecord.Insight?.ContentTags?
                    .Where(t => _featureEngine.Tags.GetEntry(t)?.Status == "unexamined")
                    .ToList() ?? new List<string>()
            );
            _tagMergeService.ExecuteDecisions(summaryResult.PendingTagsDecision, allPendingTags);
        }

        // ════════════════════════════════════════════════════════════
        // ★ 17. 批次 E：执行版本对齐（只写实际出现的字段，空值不覆盖）
        // ════════════════════════════════════════════════════════════
        if (summaryResult.TagAlignments != null && summaryResult.TagAlignments.Count > 0)
        {
            int aligned = 0;
            foreach (var kv in summaryResult.TagAlignments)
            {
                var tagName = kv.Key;
                var alignment = kv.Value;
                if (alignment == null) continue;

                // 按名查 code（走 ResolveCode，merged 自动重定向）
                var code = _featureEngine.Tags.GetCode(tagName);
                if (code == null)
                {
                    Console.WriteLine($"[CompleteAsync] ⚠️ 对齐标签不存在，跳过: {tagName}");
                    continue;
                }

                // 只写实际出现的字段
                if (!string.IsNullOrEmpty(alignment.Definition))
                {
                    _featureEngine.Tags.UpdateDefinition(code, alignment.Definition);
                    aligned++;
                }

                if (alignment.Dimensions != null && alignment.Dimensions.Count > 0)
                {
                    _featureEngine.Tags.UpdateDimensions(code, alignment.Dimensions);
                    aligned++;
                }
            }
            Console.WriteLine($"[CompleteAsync] 版本对齐完成，共 {aligned} 处写回");
        }

        Console.WriteLine($"✅ CompleteAsync: 卡片已补全 {cardId}，状态: {status}");
    }

    public async Task<string?> GetOverviewTextAsync(string sourcePath)
    {
        return await _store.ReadOverviewTextAsync(sourcePath);
    }

    // ============================================================
    // 私有辅助方法
    // ============================================================

    private async Task<string> GetPreviousOverviewAsync(string topicId)
    {
        var chatPath = _thinkingStore.GetTopicPath(topicId);
        if (!Directory.Exists(chatPath))
            return "";

        var files = Directory.GetFiles(chatPath, "记录_*.json")
            .OrderByDescending(f => f)
            .Take(2)
            .ToArray();

        if (files.Length < 2)
            return "";

        try
        {
            var prevJson = await File.ReadAllTextAsync(files[1]);
            var prevIndex = JsonSerializer.Deserialize<DiversionIndexModel>(prevJson);
            if (prevIndex?.CognitiveRecordId == null)
                return "";

            var prevCard = await _store.ReadCardAsync(prevIndex.CognitiveRecordId);
            if (prevCard?.SourcePath == null)
                return "";

            var overviewText = await _store.ReadOverviewTextAsync(prevCard.SourcePath);
            return overviewText ?? "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CompleteAsync] 取上一轮回复失败: {ex.Message}");
            return "";
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

            await _store.WriteSummaryAsync(topicId, summary);
            await _store.WriteOverviewAsync(topicId, "", overview.Text);
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
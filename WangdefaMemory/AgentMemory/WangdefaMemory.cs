using System.Text.Json;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.FeatureEngine.Models;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Knowledge;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Signal;
using Wangdefa.AgentMemory.Thinking;


namespace Wangdefa.AgentMemory;

public class WangdefaMemory : IWangdefaMemory
{
    private readonly string _cognitivePath;
    internal readonly string _recordsPath;
    internal readonly string _knowledgePath;
    internal readonly string _basePath;

    internal readonly FeatureEngine.FeatureEngine _featureEngine;
    internal readonly IThinkingStore _thinkingStore;
    private readonly CognitiveReader _cognitiveReader;
    private readonly IMemorySinkService _sinkService;
    private readonly MemoryMetadataService _metadataService;
    private readonly MemoryCleaner _cleaner;
    private readonly MaintenanceSettings _maintenanceSettings;
    private Timer? _cleanTimer;

    public WangdefaMemory(
        string basePath,
        FeatureEngine.FeatureEngine featureEngine,
        IThinkingStore thinkingStore,
        CognitiveReader cognitiveReader,
        IMemorySinkService sinkService,
        MemoryMetadataService metadataService,
        MemoryCleaner cleaner,
        MaintenanceSettings? maintenanceSettings = null)
    {
        _basePath = basePath;
        _cognitivePath = Path.Combine(basePath, "cognitive");
        _recordsPath = Path.Combine(_cognitivePath, "records");
        _knowledgePath = Path.Combine(basePath, "knowledge");
        _maintenanceSettings = maintenanceSettings ?? new MaintenanceSettings();

        Directory.CreateDirectory(_cognitivePath);
        Directory.CreateDirectory(_recordsPath);
        Directory.CreateDirectory(_knowledgePath);

        _featureEngine = featureEngine;
        _thinkingStore = thinkingStore;
        _cognitiveReader = cognitiveReader;
        _sinkService = sinkService;
        _metadataService = metadataService;
        _cleaner = cleaner;

        _cleanTimer = StartCleanTimer();
    }

    // ===== 所有 public 方法 =====
    public async Task<CognitiveMatchResultModel?> CognitiveMatch(string input, List<string>? history = null, string? topicId = null)
        => await _cognitiveReader.Match(input, history ?? new List<string>(), topicId);

    public async Task<CognitiveMatchResultModel?> CognitiveMatch(string input, string[]? semanticTags, List<string>? history = null, string? topicId = null)
    {
        if (semanticTags != null && semanticTags.Length > 0)
        {
            return await _cognitiveReader.Match(input, semanticTags, history ?? new List<string>(), topicId);
        }
        return await _cognitiveReader.Match(input, history ?? new List<string>(), topicId);
    }

    public async Task<CognitiveMatchResultModel?> CognitiveMatchByCodes(List<string> codes, string? topicId = null)
    {
        if (codes == null || codes.Count == 0)
            return null;
        return await _cognitiveReader.MatchByCodes(codes, topicId);
    }

    public async Task<List<CognitiveMatchResultModel>> CognitiveMatchTopN(string input, List<string>? history = null, string? topicId = null, int topN = 3)
        => await _cognitiveReader.MatchTopN(input, history ?? new List<string>(), topicId, topN);

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
        Dictionary<string, string>? missingTagDefinitions = null)
        => await _sinkService.SinkAsync(userInput, agentResponse, topicId, perception, summary, overview, tags, route, sourcePath, sourceType, missingTagDefinitions);

    public async Task SaveMetadataAsync(string topicId, string sourcePath, string sourceType, string fileName, long fileSize, string fileHash, string mimeType = "", string status = "pending")
        => await _metadataService.SaveMetadataAsync(topicId, sourcePath, sourceType, fileName, fileSize, fileHash, mimeType, status);

    public async Task<string> GetSourcePathAsync(string topicId, string recordId)
        => await _metadataService.GetSourcePathAsync(topicId, recordId);

    public async Task UpdateMetadataStatusAsync(string topicId, string fileHash, string status)
        => await _metadataService.UpdateMetadataStatusAsync(topicId, fileHash, status);

    public async Task<DiversionIndexModel?> DeepSearch(string recordId, string? topicId = null)
        => await _thinkingStore.LoadIndex(recordId, topicId);

    public IThinkingStore GetThinkingStore() => _thinkingStore;

    public async Task<int> CleanMemoryAsync() => await _cleaner.CleanAsync();

    public void ResetCleanTimer()
    {
        _cleanTimer?.Dispose();
        _cleanTimer = StartCleanTimer();
    }

    public async Task<string?> GetOverview(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath)) return null;

        var fullPath = Path.Combine(_basePath, sourcePath);
        if (!File.Exists(fullPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(fullPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Text", out var text))
            {
                return text.GetString();
            }
            if (doc.RootElement.TryGetProperty("text", out var textLower))
            {
                return textLower.GetString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetFullText(string recordId)
    {
        if (string.IsNullOrEmpty(recordId)) return null;

        var eventModel = await _thinkingStore.LoadEvent(recordId);
        if (eventModel == null) return null;

        return eventModel.Data?.AgentResponse ?? null;
    }

    public string? GetTagCode(string tag, string dimension)
        => _featureEngine.Tags.GetCode(tag, dimension);

    public TagEntry AddTag(string tag, string dimension, string definition = "")
        => _featureEngine.Tags.Add(tag, "content", definition, dimension, "auto");

    public TagEntry AddTagWithSynonyms(string tag, string dimension, string definition = "", string[]? synonyms = null)
    {
        return _featureEngine.Tags.AddWithSynonyms(tag, "content", definition, dimension, "auto", synonyms);
    }

    public TagEntry? GetTagEntryByCode(string code)
    {
        return _featureEngine.Tags.GetEntryByCode(code);
    }

    public async Task ExecuteEvolutionAsync(List<EvolutionAction> actions)
    {
        foreach (var action in actions)
        {
            try
            {
                switch (action.Action)
                {
                    case "deprecate":
                        _featureEngine.Tags.Deprecate(action.Code, action.Reason);
                        Console.WriteLine($"🔧 已弃用标签: {action.Code}, 原因: {action.Reason ?? "无"}");
                        break;

                    case "merge":
                        if (string.IsNullOrEmpty(action.TargetCode))
                        {
                            Console.WriteLine($"⚠️ 合并操作缺少 target_code: {action.Code}");
                            break;
                        }
                        _featureEngine.Tags.MergeTags(action.Code, action.TargetCode);
                        Console.WriteLine($"🔧 已合并标签: {action.Code} → {action.TargetCode}");
                        break;

                    case "split":
                        if (string.IsNullOrEmpty(action.TargetCode))
                        {
                            Console.WriteLine($"⚠️ 分裂操作缺少 target_code: {action.Code}");
                            break;
                        }
                        _featureEngine.Tags.SplitTag(action.Code, action.TargetCode, action.Reason);
                        Console.WriteLine($"🔧 已分裂标签: {action.Code} → {action.TargetCode}");
                        break;

                    default:
                        Console.WriteLine($"⚠️ 未知演化操作: {action.Action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 演化操作失败: {action.Action} {action.Code}, {ex.Message}");
            }
        }

        await Task.CompletedTask;
    }

    private Timer? StartCleanTimer()
    {
        if (_maintenanceSettings.CleanMode == "手动") return null;

        var nextRun = GetNextRunTime(_maintenanceSettings);
        var interval = GetInterval(_maintenanceSettings);

        return new Timer(
            async _ => await CleanMemoryAsync(),
            null,
            nextRun - DateTime.Now,
            interval
        );
    }

    private DateTime GetNextRunTime(MaintenanceSettings settings)
    {
        var now = DateTime.Now;
        var baseTime = new DateTime(now.Year, now.Month, now.Day, settings.CleanHour, settings.CleanMinute, 0);

        return settings.CleanMode switch
        {
            "每天" => baseTime <= now ? baseTime.AddDays(1) : baseTime,
            "每周" => baseTime.AddDays((settings.CleanDayOfWeek - (int)now.DayOfWeek + 7) % 7),
            "每月" => new DateTime(now.Year, now.Month, settings.CleanDayOfMonth, settings.CleanHour, settings.CleanMinute, 0).AddMonths(1),
            _ => now.AddMinutes(1)
        };
    }

    private TimeSpan GetInterval(MaintenanceSettings settings) => settings.CleanMode switch
    {
        "每天" => TimeSpan.FromDays(1),
        "每周" => TimeSpan.FromDays(7),
        "每月" => TimeSpan.FromDays(30),
        _ => TimeSpan.FromDays(1)
    };
}
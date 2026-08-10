using Wangdefa.AgentMemory.FeatureEngine.Models;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Interfaces;

/// <summary>
/// 老王记忆体对外接口 — 外部只依赖这个接口
/// </summary>
public interface IWangdefaMemory
{
    Task<CognitiveMatchResultModel?> CognitiveMatch(string input, List<string>? history = null, string? topicId = null);

    Task<CognitiveMatchResultModel?> CognitiveMatch(string input, string[]? semanticTags, List<string>? history = null, string? topicId = null);

    Task<CognitiveMatchResultModel?> CognitiveMatchByCodes(List<string> codes, string? topicId = null);

    Task<List<CognitiveMatchResultModel>> CognitiveMatchTopN(string input, List<string>? history = null, string? topicId = null, int topN = 3);

    Task SinkAsync(
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
        Dictionary<string, string>? missingTagDefinitions = null);

    Task SaveMetadataAsync(string topicId, string sourcePath, string sourceType, string fileName, long fileSize, string fileHash, string mimeType = "", string status = "pending");
    Task<string> GetSourcePathAsync(string topicId, string recordId);
    Task UpdateMetadataStatusAsync(string topicId, string fileHash, string status);

    Task<DiversionIndexModel?> DeepSearch(string recordId, string? topicId = null);
    IThinkingStore GetThinkingStore();
    Task<int> CleanMemoryAsync();
    void ResetCleanTimer();

    Task<string?> GetOverview(string sourcePath);
    Task<string?> GetFullText(string recordId);

    /// <summary>
    /// 获取标签 code（按 tag + dimension）
    /// </summary>
    string? GetTagCode(string tag, string dimension);

    /// <summary>
    /// 添加标签到标签池
    /// </summary>
    TagEntry AddTag(string tag, string dimension, string definition = "");

    /// <summary>
    /// 添加标签到标签池（带近义词）
    /// </summary>
    TagEntry AddTagWithSynonyms(string tag, string dimension, string definition = "", string[]? synonyms = null);

    /// <summary>
    /// 执行演化操作（写入后调用）
    /// </summary>
    Task ExecuteEvolutionAsync(List<EvolutionAction> actions);

    /// <summary>
    /// 获取标签条目（按 code）
    /// </summary>
    TagEntry? GetTagEntryByCode(string code);
}
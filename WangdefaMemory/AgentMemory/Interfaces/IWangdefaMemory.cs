using Wangdefa.AgentMemory.FeatureEngine.Models;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Interfaces;

/// <summary>
/// 老王记忆体对外接口 — 外部只依赖这个接口
/// </summary>
public interface IWangdefaMemory
{
    /// <summary>
    /// 根据输入匹配单条认知卡片
    /// </summary>
    Task<CognitiveMatchResultModel?> CognitiveMatch(string input, List<string>? history = null, string? topicId = null);

    /// <summary>
    /// 根据输入 + 语义标签匹配单条认知卡片
    /// </summary>
    Task<CognitiveMatchResultModel?> CognitiveMatch(string input, string[]? semanticTags, List<string>? history = null, string? topicId = null);

    /// <summary>
    /// 根据标签 code 列表匹配单条认知卡片
    /// </summary>
    Task<CognitiveMatchResultModel?> CognitiveMatchByCodes(List<string> codes, string? topicId = null);

    /// <summary>
    /// 匹配多条认知卡片，返回 TopN
    /// </summary>
    Task<List<CognitiveMatchResultModel>> CognitiveMatchTopN(string input, List<string>? history = null, string? topicId = null, int topN = 3);

    /// <summary>
    /// 完整写入记忆体（含 agentResponse）
    /// </summary>
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
        Dictionary<string, string>? missingTagDefinitions = null,
        List<PreferenceEntry>? preferences = null);

    /// <summary>
    /// 保存文件元数据
    /// </summary>
    Task SaveMetadataAsync(string topicId, string sourcePath, string sourceType, string fileName, long fileSize, string fileHash, string mimeType = "", string status = "pending");

    /// <summary>
    /// 获取来源路径
    /// </summary>
    Task<string> GetSourcePathAsync(string topicId, string recordId);

    /// <summary>
    /// 更新元数据状态
    /// </summary>
    Task UpdateMetadataStatusAsync(string topicId, string fileHash, string status);

    /// <summary>
    /// 深度检索（按记录ID）
    /// </summary>
    Task<DiversionIndexModel?> DeepSearch(string recordId, string? topicId = null);

    /// <summary>
    /// 获取思考层存储
    /// </summary>
    IThinkingStore GetThinkingStore();

    /// <summary>
    /// 清理低权重记忆
    /// </summary>
    Task<int> CleanMemoryAsync();

    /// <summary>
    /// 重置清理计时器
    /// </summary>
    void ResetCleanTimer();

    /// <summary>
    /// 获取概览（按路径）
    /// </summary>
    Task<string?> GetOverview(string sourcePath);

    /// <summary>
    /// 获取原文（按记录ID）
    /// </summary>
    Task<string?> GetFullText(string recordId);

    /// <summary>
    /// 获取标签 code（按 tag + dimension）
    /// </summary>
    string? GetTagCode(string tag, string dimension);

    /// <summary>
    /// 用 tag + definitions 做子串匹配，返回匹配的 code
    /// 优先级：精准匹配 > dimension匹配 > definition子串匹配
    /// </summary>
    string? GetTagCodeByTagAndDefinitions(string tag, string[] definitions, string dimension);

    /// <summary>
    /// 添加标签到标签池
    /// </summary>
    TagEntry AddTag(string tag, string dimension, string definition = "");

    /// <summary>
    /// 添加标签到标签池（带近义词）
    /// </summary>
    TagEntry AddTagWithSynonyms(string tag, string dimension, string definition = "", string[]? synonyms = null);

    /// <summary>
    /// 执行演化操作（合并/分裂/弃用）
    /// </summary>
    Task ExecuteEvolutionAsync(List<EvolutionAction> actions);

    /// <summary>
    /// 获取标签条目（按 code）
    /// </summary>
    TagEntry? GetTagEntryByCode(string code);

    /// <summary>
    /// 前置写入卡片框架（不含 agentResponse，状态为 pending）
    /// </summary>
    /// <returns>返回创建的卡片ID（cardId），用于后续补全时精确定位</returns>
    Task<string> WriteMemoryFrame(
        string topicId,
        string userInput,
        PerceptionModel perception,
        List<string> tags,
        string route,
        string? sourcePath = null,
        string? sourceType = null);

    /// <summary>
    /// 补全卡片（更新 agentResponse 和状态）
    /// </summary>
    /// <param name="cardId">卡片ID（由 WriteMemoryFrame 返回）</param>
    /// <param name="agentResponse">Agent的回复内容</param>
    /// <param name="status">状态：completed / interrupted / failed</param>
    /// <param name="errorMessage">错误信息（当状态为 failed 时可选）</param>
    Task CompleteMemory(
        string cardId,
        string agentResponse,
        string status,
        string? errorMessage = null);
}
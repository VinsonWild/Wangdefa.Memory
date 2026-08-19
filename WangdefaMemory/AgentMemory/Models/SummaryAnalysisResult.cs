// ================================================================
// SummaryAnalysisResult.cs — C 线输出数据模型
// ================================================================

using Wangdefa.AgentMemory.Cognitive;

namespace Wangdefa.AgentMemory.Models;

public class SummaryAnalysisResult
{
    public string Summary { get; set; } = "";
    public string Overview { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public StructuredTag[] StructuredTags { get; set; } = Array.Empty<StructuredTag>();
    public EvolutionAction[] EvolutionActions { get; set; } = Array.Empty<EvolutionAction>();

    /// <summary>
    /// 用户偏好列表
    /// </summary>
    public List<PreferenceEntry> Preferences { get; set; } = new();

    /// <summary>
    /// 缺失标签的 definition 填充结果（key: tag, value: definition）
    /// </summary>
    public Dictionary<string, string> MissingTagDefinitions { get; set; } = new();
}

/// <summary>
/// 用户偏好条目
/// </summary>

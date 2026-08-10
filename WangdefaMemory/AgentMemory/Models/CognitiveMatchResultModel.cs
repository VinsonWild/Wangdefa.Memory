using System.Collections.Generic;

namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 认知匹配结果 - 返回给 Agent
/// </summary>
public class CognitiveMatchResultModel
{
    public string? Summary { get; set; }
    public string[] ContentTags { get; set; } = Array.Empty<string>();
    public List<RelationTag> RelationTags { get; set; } = new();
    public string? RecordId { get; set; }
    public double Confidence { get; set; }
    public PerceptionModel? Perception { get; set; }
    public InsightModel? Insight { get; set; }

    // ===== 时间戳（用于时间衰减排序） =====
    public DateTime? CreatedAt { get; set; }

    // ===== 偏好（从 Insight 提取） =====
    public List<PreferenceEntry>? Preferences { get; set; }

    // ===== 指针字段（必须存在） =====
    public string? SummaryPointer { get; set; }
    public string? OverviewPointer { get; set; }
    public string? FullTextPointer { get; set; }
    public string? FullTextType { get; set; }
    public string? SourcePath { get; set; }

    public string ToContext()
    {
        var parts = new List<string>();

        if (ContentTags.Length > 0)
            parts.Add($"内容标签: {string.Join(", ", ContentTags)}");

        if (RelationTags.Any())
            parts.Add($"关系标签: {string.Join(", ", RelationTags.Select(r => $"{r.From}→{r.To}({r.Strength:F2})"))}");

        if (!string.IsNullOrEmpty(Summary))
            parts.Add($"摘要: {Summary}");

        if (Preferences != null && Preferences.Any())
            parts.Add($"偏好: {string.Join(", ", Preferences.Select(p => $"{p.Key}={p.Value}({p.Confidence:F0%})"))}");

        return string.Join("\n", parts);
    }
}
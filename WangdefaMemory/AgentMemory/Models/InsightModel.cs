// ================================================================
// InsightModel.cs — 见识结构（含偏好）
// ================================================================

using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 关系标签 - 带强度值（0-1）
/// </summary>
public class RelationTag
{
    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("to")]
    public string To { get; set; } = "";

    [JsonPropertyName("strength")]
    public double Strength { get; set; } = 0.5;
}

/// <summary>
/// 用户偏好条目
/// </summary>
public class PreferenceEntry
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.5;
}

/// <summary>
/// 见识结构 - 知道"怎么理解它"
/// </summary>
public class InsightModel
{
    [JsonPropertyName("内容标签")]
    public string[] ContentTags { get; set; } = Array.Empty<string>();

    [JsonPropertyName("关系标签")]
    public List<RelationTag> RelationTags { get; set; } = new();

    [JsonPropertyName("摘要")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("偏好")]
    public List<PreferenceEntry> Preferences { get; set; } = new();
}
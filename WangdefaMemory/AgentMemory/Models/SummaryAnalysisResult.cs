// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

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

    /// <summary>
    /// 标签合并决策（key: 待确认标签名, value: merge_to:xxx 或 activate）
    /// </summary>
    public Dictionary<string, string> PendingTagsDecision { get; set; } = new();

    /// <summary>
    /// 版本对齐结果（批次 E：key: 标签名, value: 补写的字段）
    /// 与 PendingTagsDecision 独立，互不干扰
    /// </summary>
    public Dictionary<string, TagAlignment> TagAlignments { get; set; } = new();

    /// <summary>
    /// 本轮反馈（独立于偏好，单次评价）
    /// </summary>
    public FeedbackEntry? Feedback { get; set; }

    public string SceneCategory { get; set; } = "";
    public string SceneSub { get; set; } = "";
}

/// <summary>
/// 反馈条目（单次评价，非长期偏好）
/// </summary>
public class FeedbackEntry
{
    /// <summary>
    /// 反馈状态：confirmed / rejected / partial / ignored
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    /// 反馈原因说明
    /// </summary>
    public string Reason { get; set; } = "";
}

/// <summary>
/// 标签对齐条目（批次 E 版本对齐层）
/// </summary>
public class TagAlignment
{
    /// <summary>补写的释义</summary>
    public string Definition { get; set; } = "";

    /// <summary>补写的维度</summary>
    public List<string> Dimensions { get; set; } = new();
}
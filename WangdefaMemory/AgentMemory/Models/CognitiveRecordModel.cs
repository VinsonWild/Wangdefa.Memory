// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 认知记录 - 一条完整的"见识"
/// </summary>
public class CognitiveRecordModel
{
    public string Id { get; set; } = "";
    public PerceptionModel Perception { get; set; } = new();
    public InsightModel Insight { get; set; } = new();
    public string RecordId { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 权重（0.3-1.0），用于排序，随时间衰减
    /// </summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// 最后被访问的时间（用于衰减计算）
    /// </summary>
    public DateTime LastAccessAt { get; set; }

    /// <summary>
    /// 指向知识层位置（概览/摘要）
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// 状态：pending / completed / interrupted / failed
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 所属会话ID
    /// </summary>
    public string TopicId { get; set; } = "";

    /// <summary>
    /// 关联的事件ID（用于补全时快速定位事件）
    /// </summary>
    public string EventId { get; set; } = "";

    /// <summary>
    /// 用户反馈状态：confirmed / rejected / partial / ignored
    /// </summary>
    public string FeedbackStatus { get; set; } = "";

    /// <summary>
    /// 用户反馈原因
    /// </summary>
    public string FeedbackReason { get; set; } = "";
}
namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 概要模型 — 知识层的结构化摘要
/// 用于快速检索和匹配，供系统/LLM做第一轮判断
/// 长度：10-30字（一句话）
/// </summary>
public class SummaryModel
{
    /// <summary>概要ID</summary>
    public string Id { get; set; } = "";

    /// <summary>所属话题ID</summary>
    public string TopicId { get; set; } = "";

    /// <summary>关联的认知记录ID</summary>
    public string CognitiveRecordId { get; set; } = "";

    /// <summary>核心关键词（2-4个）</summary>
    public string[] Keywords { get; set; } = Array.Empty<string>();

    /// <summary>实体列表（人名/地名/组织名等）</summary>
    public string[] Entities { get; set; } = Array.Empty<string>();

    /// <summary>时间范围（如 "2026-07-01 ~ 2026-09-30"）</summary>
    public string? DateRange { get; set; }

    /// <summary>一句话摘要（10-30字）</summary>
    public string Summary { get; set; } = "";

    /// <summary>置信度（0-1）</summary>
    public double Confidence { get; set; } = 0.5;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>文件最后修改时间（仅文件类型）</summary>
    public DateTime? ModifiedAt { get; set; }
}
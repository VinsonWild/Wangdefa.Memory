namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 分流索引 — 思考层的核心
/// 不存原文，只存"去哪找"
/// </summary>
public class DiversionIndexModel
{
    /// <summary>关联的认知记录ID（和认知层共用）</summary>
    public string CognitiveRecordId { get; set; } = "";

    /// <summary>事件类型：chat / file / system</summary>
    public string EventType { get; set; } = "chat";

    /// <summary>所属话题</summary>
    public string TopicId { get; set; } = "";

    /// <summary>概要指针 → 指向知识层的概要</summary>
    public string SummaryPointer { get; set; } = "";

    /// <summary>概览指针 → 指向知识层的概览</summary>
    public string OverviewPointer { get; set; } = "";

    /// <summary>全量指针 → 指向本地原文位置</summary>
    public string FullTextPointer { get; set; } = "";

    /// <summary>全量类型：file / db / url</summary>
    public string FullTextType { get; set; } = "file";

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>访问时间（供权重衰减）</summary>
    public DateTime LastAccessAt { get; set; } = DateTime.Now;
}
namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 概览模型 — 知识层的自然语言预览
/// 供LLM快速理解内容，决定是否需要取全文
/// 长度：100-300字
/// </summary>
public class OverviewModel
{
    public string Id { get; set; } = "";
    public string TopicId { get; set; } = "";
    public string CognitiveRecordId { get; set; } = "";
    public string Text { get; set; } = "";
    public string ContentType { get; set; } = "document";
    public int WordCount { get; set; }
    public double Confidence { get; set; } = 0.5;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 认知记录 - 一条完整的"见识"
/// </summary>
public class CognitiveRecordModel
{
    public string Id { get; set; } = "";                      // 认知_001
    public PerceptionModel Perception { get; set; } = new();
    public InsightModel Insight { get; set; } = new();
    public string RecordId { get; set; } = "";                // 指向思考层完整记录
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
}
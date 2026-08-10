using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 对话分析 — 从单次对话中提炼的偏好/行为模式/决策/习惯
/// </summary>
public class DialogueAnalysis
{
    public string Id { get; set; } = "";
    public string TopicId { get; set; } = "";
    public string Type { get; set; } = "";           // 偏好/行为模式/决策/习惯
    public string Summary { get; set; } = "";
    public DialogueAnalysisDetails Details { get; set; } = new();
    public string[] Tags { get; set; } = Array.Empty<string>();
    public List<RelationTag> RelationTags { get; set; } = new();
    public double Confidence { get; set; } = 0.5;
    public List<string> SourceEventIds { get; set; } = new();
    public double Weight { get; set; } = 1.0;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessAt { get; set; }
}

public class DialogueAnalysisDetails
{
    public string Trigger { get; set; } = "";
    public string Action { get; set; } = "";
    public string Result { get; set; } = "";
}

/// <summary>
/// LLM 提取结果（中间格式）
/// </summary>
public class DialogueAnalysisResult
{
    public string Type { get; set; } = "";
    public string Summary { get; set; } = "";
    public DialogueAnalysisDetails Details { get; set; } = new();
    public string[] Tags { get; set; } = Array.Empty<string>();
    public List<RelationTag> RelationTags { get; set; } = new();
    public double Confidence { get; set; } = 0.5;
}
namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 用户反馈 - 评价上一条记忆是否命中
/// </summary>
public class UserFeedback
{
    public string Status { get; set; } = "pending";  // pending / confirmed / rejected / ignored
    public string? JudgedBy { get; set; }            // 下一条记录的ID
    public DateTime? JudgedAt { get; set; }          // 评价时间
    public string? Reason { get; set; }              // 评价原因（可选）
}

/// <summary>
/// 聊天记录（思考层-对话原文）
/// </summary>
public class ChatRecord
{
    public string Id { get; set; } = "";                     // 记录_xxx
    public string TopicId { get; set; } = "";
    public string UserInput { get; set; } = "";
    public string AgentResponse { get; set; } = "";
    public string[] CognitiveTags { get; set; } = Array.Empty<string>();
    public string CognitiveSummary { get; set; } = "";
    public double Confidence { get; set; } = 0.0;
    public string[] Candidates { get; set; } = Array.Empty<string>();  // 本次检索到的候选记录ID
    public UserFeedback UserFeedback { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 归档文件结构
/// </summary>
public class ArchiveFile
{
    public string TopicId { get; set; } = "";
    public string Period { get; set; } = "";
    public DateTime MergedAt { get; set; }
    public List<ChatRecord> Records { get; set; } = new();
}
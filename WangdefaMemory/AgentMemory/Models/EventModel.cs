using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 统一事件模型 — 所有类型事件共用
/// </summary>
public class EventModel
{
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";      // chat / file / action / task / system
    public string EventLevel { get; set; } = "point"; // point / step
    public string? ParentEventId { get; set; }        // 如果是 step，指向父 point
    public string Mode { get; set; } = "wangdefa_full";
    public string TopicId { get; set; } = "";
    public DateTime Timestamp { get; set; }

    // ===== 感知（从 Harness 输出直接存入） =====
    public PerceptionModel? Perception { get; set; }

    public EventData Data { get; set; } = new();
    public EventContext Context { get; set; } = new();
    public EventResult Result { get; set; } = new();

    public string? CognitiveRecordId { get; set; }    // 关联认知层
    public string[] FeatureTags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 闭环产出：从事件中提炼的对话分析结果
    /// </summary>
    public DialogueAnalysis? ExtractedInsight { get; set; }
}

public class EventData
{
    // 对话
    public string? UserInput { get; set; }
    public string? AgentResponse { get; set; }

    // 文件
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public string? FileAction { get; set; }           // upload / scan / open / save / delete

    // 行为/任务
    public string? ActionType { get; set; }           // tool_call / button_click / mode_switch
    public string? TaskName { get; set; }
    public List<EventStep>? Steps { get; set; }       // point 下的子步骤

    // 系统
    public string? SystemEvent { get; set; }          // startup / shutdown / config_change

    // 扩展
    public Dictionary<string, object>? Extra { get; set; }
}

public class EventStep
{
    public int Step { get; set; }
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class EventContext
{
    public string? SessionId { get; set; }
    public string? UserId { get; set; }
    public string[]? RelatedEventIds { get; set; }
    public string? Source { get; set; }               // console / http / plugin / system
}

public class EventResult
{
    public string Status { get; set; } = "pending";   // pending / completed / failed / partial
    public string? Summary { get; set; }
    public string? Route { get; set; }                // shallow / medium / deep
    public string? ErrorMessage { get; set; }
    public double? DurationMs { get; set; }
    public string? UserRating { get; set; }           // useful / useless
}
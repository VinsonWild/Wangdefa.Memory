using System.Text.Json;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;

namespace Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;

/// <summary>
/// 任务提炼器 - 从任务执行中提炼工作流模式
/// </summary>
public class TaskExtractor
{
    private readonly IChatService _chatService;

    public TaskExtractor(IChatService chatService)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService), "TaskExtractor 必须配置 ChatService");
    }

    /// <summary>
    /// 从单个任务事件中提炼洞察
    /// </summary>
    public async Task<DialogueAnalysis?> ExtractAsync(EventModel evt)
    {
        var taskName = evt.Data.TaskName;
        var steps = evt.Data.Steps;
        var resultSummary = evt.Result.Summary;

        if (string.IsNullOrEmpty(taskName))
            return null;

        try
        {
            var stepsInfo = steps != null && steps.Count > 0
                ? $"执行步骤：\n{string.Join("\n", steps.Select((s, i) => $"  {i + 1}. {s.Action}: {s.Result ?? "进行中"}"))}"
                : "（无详细步骤）";

            var prompt = $@"
分析以下任务执行记录，提炼工作流模式或常用任务类型。

任务名称：{taskName}
任务结果：{resultSummary ?? "未知"}
{stepsInfo}

返回 JSON 格式：
{{
    ""type"": ""工作流模式/常用任务"",
    ""summary"": ""一句话总结这个任务模式"",
    ""details"": {{
        ""trigger"": ""触发条件"",
        ""action"": ""执行流程"",
        ""result"": ""典型结果""
    }},
    ""tags"": [""标签1"", ""标签2""],
    ""relation_tags"": [
        {{ ""from"": ""任务"", ""to"": ""目标"", ""strength"": 0.9 }}
    ],
    ""confidence"": 0.8
}}

要求：
- 如果是单次、临时任务，返回 null
- 只提炼可重复的工作流模式
- 只返回 JSON，不要其他内容";

            var result = await _chatService.ChatAsync(prompt);
            if (string.IsNullOrEmpty(result)) return null;

            var json = ExtractJson(result);
            if (string.IsNullOrEmpty(json)) return null;

            var extraction = System.Text.Json.JsonSerializer.Deserialize<DialogueAnalysisResult>(json);
            if (extraction == null || string.IsNullOrEmpty(extraction.Type) || extraction.Type == "null")
                return null;

            return new DialogueAnalysis
            {
                Id = $"分析_{DateTime.Now:yyyyMMdd_HHmmss}",
                TopicId = evt.TopicId,
                Type = "工作流模式",
                Summary = extraction.Summary ?? $"常用任务：{taskName}",
                Details = extraction.Details ?? new DialogueAnalysisDetails(),
                Tags = extraction.Tags ?? new[] { "任务", taskName },
                RelationTags = extraction.RelationTags ?? new List<RelationTag>(),
                Confidence = extraction.Confidence,
                SourceEventIds = new List<string> { evt.EventId },
                Weight = 1.0,
                CreatedAt = DateTime.Now,
                LastAccessAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 任务提炼失败: {ex.Message}");
            return null;
        }
    }

    private string ExtractJson(string text)
    {
        if (string.IsNullOrEmpty(text)) return null!;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start && end > start + 1)
        {
            return text.Substring(start, end - start + 1);
        }
        return null!;
    }
}
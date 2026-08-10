using System.Text.Json;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;

namespace Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;

/// <summary>
/// 行为提炼器 - 从用户行为中提炼模式/习惯
/// </summary>
public class ActionExtractor
{
    private readonly IChatService _chatService;

    public ActionExtractor(IChatService chatService)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService), "ActionExtractor 必须配置 ChatService");
    }

    /// <summary>
    /// 从单个行为事件中提炼洞察
    /// </summary>
    public async Task<DialogueAnalysis?> ExtractAsync(EventModel evt)
    {
        var actionType = evt.Data.ActionType;
        var extra = evt.Data.Extra;

        if (string.IsNullOrEmpty(actionType))
            return null;

        try
        {
            var extraInfo = extra != null
                ? $"额外信息：{JsonSerializer.Serialize(extra)}"
                : "";

            var prompt = $@"
分析以下用户行为，提炼行为模式或习惯。如果是常见操作，返回 null。

行为类型：{actionType}
时间：{evt.Timestamp:yyyy-MM-dd HH:mm}
{extraInfo}

返回 JSON 格式：
{{
    ""type"": ""行为模式/习惯"",
    ""summary"": ""一句话总结这个行为模式"",
    ""details"": {{
        ""trigger"": ""触发条件"",
        ""action"": ""具体行为"",
        ""result"": ""行为结果或影响""
    }},
    ""tags"": [""标签1"", ""标签2""],
    ""relation_tags"": [
        {{ ""from"": ""用户"", ""to"": ""目标"", ""strength"": 0.9 }}
    ],
    ""confidence"": 0.8
}}

要求：
- 如果是单一、偶发的行为（如打开一次文件），返回 null
- 只有重复性、模式化的行为才提炼
- 只返回 JSON，不要其他内容";

            var result = await _chatService.ChatAsync(prompt);
            if (string.IsNullOrEmpty(result)) return null;

            var json = ExtractJson(result);
            if (string.IsNullOrEmpty(json)) return null;

            var extraction = JsonSerializer.Deserialize<DialogueAnalysisResult>(json);
            if (extraction == null || string.IsNullOrEmpty(extraction.Type) || extraction.Type == "null")
                return null;

            return new DialogueAnalysis
            {
                Id = $"分析_{DateTime.Now:yyyyMMdd_HHmmss}",
                TopicId = evt.TopicId,
                Type = "行为模式",
                Summary = extraction.Summary ?? $"用户常做：{actionType}",
                Details = extraction.Details ?? new DialogueAnalysisDetails(),
                Tags = extraction.Tags ?? new[] { "行为", actionType },
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
            Console.WriteLine($"⚠️ 行为提炼失败: {ex.Message}");
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
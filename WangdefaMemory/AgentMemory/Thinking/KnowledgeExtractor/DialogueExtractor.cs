using System.Text.Json;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;

namespace Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;

/// <summary>
/// 对话提炼器 - 从对话记录中提取行为/偏好/决策
/// </summary>
public class DialogueExtractor
{
    private readonly IChatService _chatService;

    public DialogueExtractor(IChatService chatService)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService), "DialogueExtractor 必须配置 ChatService");
    }

    /// <summary>
    /// 从单条对话事件中提取洞察
    /// </summary>
    public async Task<DialogueAnalysis?> ExtractAsync(EventModel evt)
    {
        var userInput = evt.Data.UserInput;
        var agentResponse = evt.Data.AgentResponse;
        var topicId = evt.TopicId;

        if (string.IsNullOrEmpty(userInput))
            return null;

        try
        {
            var prompt = $@"
分析以下对话，提取用户的行为模式、偏好、决策或习惯。如果没有可提取的知识，返回 null。

用户输入：{userInput}
Agent 回复：{agentResponse}
话题ID：{topicId}

返回 JSON 格式：
{{
    ""type"": ""行为模式/偏好/决策/习惯"",
    ""summary"": ""一句话总结这个知识"",
    ""details"": {{
        ""trigger"": ""触发条件"",
        ""action"": ""具体行为"",
        ""result"": ""结果或影响""
    }},
    ""tags"": [""标签1"", ""标签2"", ""标签3""],
    ""relation_tags"": [
        {{ ""from"": ""来源"", ""to"": ""目标"", ""strength"": 0.9 }}
    ],
    ""confidence"": 0.8
}}

要求：
- 如果对话只是闲聊、无实质内容，返回 null
- 标签 2-4 个，要能代表这个知识的核心
- 关系标签 1-3 个，带强度值 0-1
- 置信度表示你有多确定这个知识是正确的
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
                TopicId = topicId,
                Type = extraction.Type,
                Summary = extraction.Summary ?? "",
                Details = extraction.Details ?? new DialogueAnalysisDetails(),
                Tags = extraction.Tags ?? Array.Empty<string>(),
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
            Console.WriteLine($"⚠️ 对话提炼失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从多条对话记录中聚合提取洞察（用于批量处理）
    /// </summary>
    public async Task<DialogueAnalysis?> ExtractFromEvents(List<EventModel> events)
    {
        if (events.Count == 0) return null;

        var combinedInput = string.Join("\n---\n", events.Select((e, i) =>
            $"记录{i + 1}:\n用户：{e.Data.UserInput}\nAgent：{e.Data.AgentResponse}"));

        var topicId = events.FirstOrDefault()?.TopicId ?? "default";

        try
        {
            var prompt = $@"
分析以下 {events.Count} 条对话记录，提取用户的行为模式、偏好、决策或习惯。

{combinedInput}

返回 JSON 格式：
{{
    ""type"": ""行为模式/偏好/决策/习惯"",
    ""summary"": ""一句话总结这个知识"",
    ""details"": {{
        ""trigger"": ""触发条件"",
        ""action"": ""具体行为"",
        ""result"": ""结果或影响""
    }},
    ""tags"": [""标签1"", ""标签2"", ""标签3""],
    ""relation_tags"": [
        {{ ""from"": ""来源"", ""to"": ""目标"", ""strength"": 0.9 }}
    ],
    ""confidence"": 0.8
}}

要求：
- 提取跨记录的共同模式
- 如果无共同模式，返回 null
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
                TopicId = topicId,
                Type = extraction.Type,
                Summary = extraction.Summary ?? "",
                Details = extraction.Details ?? new DialogueAnalysisDetails(),
                Tags = extraction.Tags ?? Array.Empty<string>(),
                RelationTags = extraction.RelationTags ?? new List<RelationTag>(),
                Confidence = extraction.Confidence,
                SourceEventIds = events.Select(e => e.EventId).ToList(),
                Weight = 1.0,
                CreatedAt = DateTime.Now,
                LastAccessAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 批量对话提炼失败: {ex.Message}");
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
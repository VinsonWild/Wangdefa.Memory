using System.Text.Json;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;

namespace Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;

/// <summary>
/// 文件提炼器 - 从文件中提炼主题/关键词/实体
/// </summary>
public class FileExtractor
{
    private readonly IChatService _chatService;

    public FileExtractor(IChatService chatService)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService), "FileExtractor 必须配置 ChatService");
    }

    /// <summary>
    /// 从单个文件事件中提炼洞察
    /// </summary>
    public async Task<DialogueAnalysis?> ExtractAsync(EventModel evt)
    {
        var filePath = evt.Data.FilePath;
        var fileName = evt.Data.FileName;
        var fileAction = evt.Data.FileAction;

        if (string.IsNullOrEmpty(fileName))
            return null;

        // 尝试读取文件内容（如果是文本文件）
        string? fileContent = null;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath);
                if (IsTextFile(bytes))
                {
                    fileContent = await File.ReadAllTextAsync(filePath);
                    if (fileContent.Length > 5000)
                        fileContent = fileContent.Substring(0, 5000) + "...";
                }
            }
            catch
            {
                // 读取失败则跳过内容
            }
        }

        try
        {
            var contentSection = string.IsNullOrEmpty(fileContent)
                ? "（无法读取文件内容）"
                : $"文件内容摘要：\n{fileContent}";

            var prompt = $@"
分析以下文件信息，提炼文件的核心主题、关键词和实体。如果没有可提炼的内容，返回 null。

文件名：{fileName}
文件类型：{Path.GetExtension(filePath ?? fileName)}
操作类型：{fileAction ?? "未知"}
{contentSection}

返回 JSON 格式：
{{
    ""type"": ""文件主题"",
    ""summary"": ""一句话总结这个文件的核心内容"",
    ""details"": {{
        ""trigger"": ""文件来源或触发场景"",
        ""action"": ""用户对文件的操作"",
        ""result"": ""文件的主要内容或用途""
    }},
    ""tags"": [""标签1"", ""标签2"", ""标签3""],
    ""relation_tags"": [
        {{ ""from"": ""文件"", ""to"": ""目标实体"", ""strength"": 0.9 }}
    ],
    ""confidence"": 0.8
}}

要求：
- 如果文件是二进制文件且无法读取内容，基于文件名和路径推断主题
- 标签 2-4 个，代表文件的核心主题
- 只返回 JSON，不要其他内容";

            var result = await _chatService.ChatAsync(prompt);
            if (string.IsNullOrEmpty(result)) return null;

            var json = ExtractJson(result);
            if (string.IsNullOrEmpty(json)) return null;

            var extraction = JsonSerializer.Deserialize<DialogueAnalysisResult>(json);
            if (extraction == null || string.IsNullOrEmpty(extraction.Type))
                return null;

            return new DialogueAnalysis
            {
                Id = $"分析_{DateTime.Now:yyyyMMdd_HHmmss}",
                TopicId = evt.TopicId,
                Type = "文件主题",
                Summary = extraction.Summary ?? $"文件：{fileName}",
                Details = extraction.Details ?? new DialogueAnalysisDetails(),
                Tags = extraction.Tags ?? new[] { "文件", Path.GetExtension(filePath ?? fileName).TrimStart('.') },
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
            Console.WriteLine($"⚠️ 文件提炼失败: {ex.Message}");
            return null;
        }
    }

    private bool IsTextFile(byte[] bytes)
    {
        var sample = bytes.Take(Math.Min(1000, bytes.Length)).ToArray();
        foreach (var b in sample)
        {
            if (b < 0x09 || (b > 0x0D && b < 0x20 && b != 0x1B))
                return false;
        }
        return true;
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
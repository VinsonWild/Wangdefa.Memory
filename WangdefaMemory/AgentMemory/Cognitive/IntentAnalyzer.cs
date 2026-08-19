// ================================================================
// IntentAnalyzer.cs — A 线：意图分析（含三层兜底）
// ================================================================

using System.Text.Json;
using Wangdefa.Contracts;
using Wangdefa.AgentMemory.Models;
using WangdefaMemory.AgentMemory;

namespace Wangdefa.AgentMemory.Cognitive;

/// <summary>
/// A 线：意图分析执行器
/// </summary>
public class IntentAnalyzer
{
    private readonly IChatService _chatService;
    private readonly string _instruction;

    public IntentAnalyzer(IChatService chatService)
    {
        _chatService = chatService;
        _instruction = PromptTemplates.GetIntentAnalysis();
    }

    public async Task<IntentAnalysisResult> AnalyzeAsync(string input, string sessionId)
    {
        Console.WriteLine("[IntentAnalyzer] 1. 进入 A 线");

        // ===== 第一层：完整意图分析 =====
        var prompt = _instruction.Replace("{userInput}", input);

        // ★ 调试日志：打印 prompt 末尾 500 字符，确认 {userInput} 是否被替换
        Console.WriteLine($"[IntentAnalyzer] Prompt 末尾 500 字符: {prompt.Substring(Math.Max(0, prompt.Length - 500))}");

        var reply = await _chatService.ChatAsync(prompt);

        if (!string.IsNullOrEmpty(reply))
        {
            try
            {
                var result = HarnessResponseParser.ParseIntentOutput(reply);
                if (result.StructuredTags != null && result.StructuredTags.Length > 0)
                {
                    Console.WriteLine($"[IntentAnalyzer] 第一层成功，提取到 {result.StructuredTags.Length} 个标签");
                    return result;
                }
                Console.WriteLine("[IntentAnalyzer] 第一层返回了空标签，尝试第二层");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IntentAnalyzer] 第一层解析失败: {ex.Message}，尝试第二层");
            }
        }

        // ===== 第二层：简化分词（调用 LLM 只提取关键词）=====
        var fallbackTags = await ExtractKeywordsFallback(input);
        if (fallbackTags.Length > 0)
        {
            Console.WriteLine($"[IntentAnalyzer] 第二层成功，提取到 {fallbackTags.Length} 个标签");
            var result = GetDefaultResult();
            result.StructuredTags = fallbackTags;
            result.ContextSummary = input.Length > 30 ? input.Substring(0, 30) : input;
            result.Intent = "查询";
            return result;
        }

        // ===== 第三层：规则分词（纯本地，100% 保底）=====
        Console.WriteLine("[IntentAnalyzer] 第二层失败，使用规则分词兜底");
        var ruleTags = ExtractTagsByRules(input);
        var defaultResult = GetDefaultResult();
        defaultResult.StructuredTags = ruleTags;
        defaultResult.ContextSummary = input.Length > 30 ? input.Substring(0, 30) : input;
        defaultResult.Intent = "查询";
        return defaultResult;
    }

    /// <summary>
    /// 第二层：调用 LLM 只提取关键词
    /// </summary>
    private async Task<StructuredTag[]> ExtractKeywordsFallback(string input)
    {
        var prompt = $"从以下文本中提取 3-5 个关键词或短语，只返回 JSON 数组，不要其他内容：\n{input}";
        var reply = await _chatService.ChatAsync(prompt);
        if (string.IsNullOrEmpty(reply)) return Array.Empty<StructuredTag>();

        try
        {
            var keywords = JsonSerializer.Deserialize<string[]>(reply) ?? Array.Empty<string>();
            return keywords.Select(k => new StructuredTag
            {
                Tag = k,
                Dimension = "内容",
                Code = "",
                Definitions = Array.Empty<string>(),
                Action = "add",
                Synonyms = Array.Empty<string>()
            }).ToArray();
        }
        catch
        {
            return Array.Empty<StructuredTag>();
        }
    }

    /// <summary>
    /// 第三层：规则分词（纯本地兜底）
    /// </summary>
    private static StructuredTag[] ExtractTagsByRules(string input)
    {
        var separators = new[] { ' ', '，', '。', '、', '！', '？', ',', '.', '!', '?', '\n', '\r', '\t', ';', '：', '；' };
        var words = input.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        var tags = new List<string>();
        foreach (var word in words)
        {
            var trimmed = word.Trim();
            if (trimmed.Length >= 2 && !trimmed.All(char.IsDigit) && trimmed.Any(char.IsLetter))
            {
                tags.Add(trimmed);
            }
        }

        tags = tags.Distinct().ToList();

        return tags.Select(t => new StructuredTag
        {
            Tag = t,
            Dimension = "内容",
            Code = "",
            Definitions = Array.Empty<string>(),
            Action = "add",
            Synonyms = Array.Empty<string>()
        }).ToArray();
    }

    private static IntentAnalysisResult GetDefaultResult()
    {
        return new IntentAnalysisResult
        {
            Intent = "闲聊",
            Route = "shallow",
            Perception = new PerceptionModel(),
            ContextSummary = "",
            NeedTools = false,
            ToolNames = Array.Empty<string>(),
            SemanticTags = Array.Empty<string>()
        };
    }
}
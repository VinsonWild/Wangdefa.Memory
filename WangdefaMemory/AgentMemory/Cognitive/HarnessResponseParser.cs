// ================================================================
// HarnessResponseParser.cs — 结构化输出解析（纯静态工具类）
// ================================================================

using System.Text.Json;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Cognitive;

public static class HarnessResponseParser
{
    public static HarnessStructuredResult ParseStructuredOutput(string rawOutput)
    {
        var result = new HarnessStructuredResult
        {
            RawReply = rawOutput,
            Reply = rawOutput,
            Perception = new PerceptionModel(),
            Route = "shallow",
            Summary = "",
            Overview = "",
            Tags = Array.Empty<string>()
        };

        try
        {
            var jsonStart = rawOutput.IndexOf('{');
            var jsonEnd = rawOutput.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = rawOutput.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("reply", out var reply))
                    result.Reply = reply.GetString() ?? rawOutput;

                if (root.TryGetProperty("perception", out var perception))
                {
                    result.Perception = new PerceptionModel
                    {
                        Genre = perception.TryGetProperty("Genre", out var g) ? g.GetString() ?? "" : "",
                        Scene = perception.TryGetProperty("Scene", out var s) ? s.GetString() ?? "" : "",
                        Emotion = perception.TryGetProperty("Emotion", out var e) ? e.GetString() ?? "" : "",
                        State = perception.TryGetProperty("State", out var st) ? st.GetString() ?? "" : "",
                        Context = perception.TryGetProperty("Context", out var c) ? c.GetString() ?? "" : ""
                    };
                }

                if (root.TryGetProperty("route", out var route))
                    result.Route = route.GetString() ?? "shallow";

                if (root.TryGetProperty("summary", out var summary))
                    result.Summary = summary.GetString() ?? "";

                if (root.TryGetProperty("overview", out var overview))
                    result.Overview = overview.GetString() ?? "";

                if (root.TryGetProperty("semantic_tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    var tagList = new List<string>();
                    foreach (var tag in tags.EnumerateArray())
                        tagList.Add(tag.GetString() ?? "");
                    result.Tags = tagList.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HarnessResponseParser] 解析失败: {ex.Message}");
        }

        return result;
    }

    public static IntentAnalysisResult ParseIntentOutput(string rawOutput)
    {
        var result = new IntentAnalysisResult
        {
            Perception = new PerceptionModel(),
            Route = "shallow",
            Intent = "闲聊",
            ContextSummary = "",
            NeedTools = false,
            ToolNames = Array.Empty<string>(),
            SemanticTags = Array.Empty<string>(),
            StructuredTags = Array.Empty<StructuredTag>(),
            MemoryInjectionMode = "off"
        };

        if (string.IsNullOrEmpty(rawOutput))
            return result;

        try
        {
            var jsonStart = rawOutput.IndexOf('{');
            var jsonEnd = rawOutput.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = rawOutput.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("perception", out var perception))
                {
                    result.Perception = new PerceptionModel
                    {
                        Genre = perception.TryGetProperty("Genre", out var g) ? g.GetString() ?? "" : "",
                        Scene = perception.TryGetProperty("Scene", out var s) ? s.GetString() ?? "" : "",
                        Emotion = perception.TryGetProperty("Emotion", out var e) ? e.GetString() ?? "" : "",
                        State = perception.TryGetProperty("State", out var st) ? st.GetString() ?? "" : "",
                        Context = perception.TryGetProperty("Context", out var c) ? c.GetString() ?? "" : ""
                    };
                }

                if (root.TryGetProperty("route", out var route))
                    result.Route = route.GetString() ?? "shallow";

                if (root.TryGetProperty("intent", out var intent))
                    result.Intent = intent.GetString() ?? "闲聊";

                if (root.TryGetProperty("context_summary", out var summary))
                    result.ContextSummary = summary.GetString() ?? "";

                if (root.TryGetProperty("need_tools", out var needTools))
                    result.NeedTools = needTools.GetBoolean();

                if (root.TryGetProperty("memory_injection_mode", out var mode))
                {
                    result.MemoryInjectionMode = mode.GetString() ?? "off";
                    Console.WriteLine($"[HarnessResponseParser] 记忆注入模式: {result.MemoryInjectionMode}");
                }

                if (root.TryGetProperty("tool_names", out var toolNames) && toolNames.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in toolNames.EnumerateArray())
                    {
                        var name = item.GetString();
                        if (!string.IsNullOrEmpty(name))
                            list.Add(name);
                    }
                    result.ToolNames = list.ToArray();
                    Console.WriteLine($"[HarnessResponseParser] 解析到工具名: {string.Join(", ", result.ToolNames)}");
                }

                // ★★★ structured_tags 解析 ★★★
                if (root.TryGetProperty("structured_tags", out var structuredTags) && structuredTags.ValueKind == JsonValueKind.Array)
                {
                    var tagList = new List<StructuredTag>();
                    foreach (var item in structuredTags.EnumerateArray())
                    {
                        var tagName = item.TryGetProperty("tag", out var t) ? t.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(tagName))
                            continue;

                        string dimension = "";
                        if (item.TryGetProperty("dimension", out var dim))
                        {
                            if (dim.ValueKind == JsonValueKind.String)
                            {
                                dimension = dim.GetString() ?? "";
                            }
                            else if (dim.ValueKind == JsonValueKind.Array)
                            {
                                var dims = new List<string>();
                                foreach (var d in dim.EnumerateArray())
                                {
                                    if (d.ValueKind == JsonValueKind.String)
                                        dims.Add(d.GetString() ?? "");
                                }
                                dimension = string.Join(",", dims);
                            }
                        }

                        var tag = new StructuredTag
                        {
                            Tag = tagName,
                            Dimension = dimension,
                            Code = item.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "",
                            // ★ 正常解析 definitions，不丢弃
                            Definitions = item.TryGetProperty("definitions", out var defs) && defs.ValueKind == JsonValueKind.Array
                                ? defs.EnumerateArray().Select(d => d.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray()
                                : (item.TryGetProperty("definition", out var singleDef) ? new[] { singleDef.GetString() ?? "" } : Array.Empty<string>()),
                            Action = item.TryGetProperty("action", out var a) ? a.GetString() ?? "add" : "add",
                            Synonyms = Array.Empty<string>()
                        };

                        if (item.TryGetProperty("synonyms", out var synonyms) && synonyms.ValueKind == JsonValueKind.Array)
                        {
                            var synList = new List<string>();
                            foreach (var syn in synonyms.EnumerateArray())
                                synList.Add(syn.GetString() ?? "");
                            tag.Synonyms = synList.ToArray();
                        }

                        tagList.Add(tag);
                        Console.WriteLine($"[HarnessResponseParser] 解析到标签: {tagName}");
                    }
                    result.StructuredTags = tagList.ToArray();
                }

                // ★★★ 只有 structured_tags 完全为空时，才走 semantic_tags 兜底 ★★★
                if (result.StructuredTags.Length == 0 && root.TryGetProperty("semantic_tags", out var semanticTags) && semanticTags.ValueKind == JsonValueKind.Array)
                {
                    var tagList = new List<StructuredTag>();
                    foreach (var item in semanticTags.EnumerateArray())
                    {
                        var tagText = item.GetString() ?? "";
                        if (!string.IsNullOrEmpty(tagText))
                        {
                            tagList.Add(new StructuredTag
                            {
                                Tag = tagText,
                                Dimension = "",
                                Code = "",
                                Definitions = Array.Empty<string>(),
                                Action = "add",
                                Synonyms = Array.Empty<string>()
                            });
                            Console.WriteLine($"[HarnessResponseParser] semantic_tags 兜底: {tagText}");
                        }
                    }
                    result.StructuredTags = tagList.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HarnessResponseParser] 解析意图失败: {ex.Message}");
        }

        Console.WriteLine($"[HarnessResponseParser] 最终 StructuredTags: {string.Join(", ", result.StructuredTags.Select(t => t.Tag))}");

        return result;
    }
}

/// <summary>
/// Harness 结构化输出结果（回复生成）
/// </summary>
public class HarnessStructuredResult
{
    public string RawReply { get; set; } = "";
    public string Reply { get; set; } = "";
    public PerceptionModel Perception { get; set; } = new();
    public string Route { get; set; } = "shallow";
    public string Summary { get; set; } = "";
    public string Overview { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 结构化标签
/// </summary>
public class StructuredTag
{
    public string Tag { get; set; } = "";
    public string Dimension { get; set; } = "";
    public string Code { get; set; } = "";
    public string[] Definitions { get; set; } = Array.Empty<string>();
    public string Action { get; set; } = "add";
    public string[] Synonyms { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 意图分析结果
/// </summary>
public class IntentAnalysisResult
{
    public PerceptionModel Perception { get; set; } = new();
    public string Route { get; set; } = "shallow";
    public string Intent { get; set; } = "闲聊";
    public string ContextSummary { get; set; } = "";
    public bool NeedTools { get; set; } = false;
    public string[] ToolNames { get; set; } = Array.Empty<string>();
    public string[] SemanticTags { get; set; } = Array.Empty<string>();
    public string Overview { get; set; } = "";
    public StructuredTag[] StructuredTags { get; set; } = Array.Empty<StructuredTag>();
    public string MemoryInjectionMode { get; set; } = "off";
}
// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

// ================================================================
// SummaryAnalyzer.cs — C 线：摘要分析（含偏好提取 + 反馈判断 + 标签合并）
// ================================================================

using System.Text.Json;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine.Models;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;
using WangdefaMemory.AgentMemory;

namespace Wangdefa.AgentMemory.Thinking;

public class SummaryAnalyzer
{
    private readonly IChatService _chatService;
    private readonly string _instruction;

    public SummaryAnalyzer(IChatService chatService)
    {
        _chatService = chatService;
        _instruction = PromptTemplates.GetSummaryAnalysis();
    }

    public async Task<SummaryAnalysisResult> AnalyzeAsync(
        string userInput,
        string agentResponse,
        string previousAgentResponse,
        StructuredTag[]? structuredTags = null,
        StructuredTag[]? missingTags = null,
        List<TagEntry>? pendingTags = null)  
    {
        Console.WriteLine("[SummaryAnalyzer] 执行 C 线摘要分析...");

        var structuredTagsText = structuredTags != null && structuredTags.Length > 0
            ? string.Join(", ", structuredTags.Select(t => $"{t.Tag}({t.Dimension})"))
            : "（无）";

        var missingTagsText = missingTags != null && missingTags.Length > 0
            ? string.Join(", ", missingTags.Select(t => $"{t.Tag}({t.Dimension})"))
            : "（无）";

        // ★ 格式化 pending 标签
        var pendingTagsText = pendingTags != null && pendingTags.Count > 0
            ? string.Join("\n", pendingTags.Select(t =>
                $"- {t.Tag}（释义：{t.Definition ?? "无"}，近义词：{t.Synonyms ?? "无"}）"))
            : "（无待确认标签）";

        var prompt = _instruction
            .Replace("{userInput}", userInput)
            .Replace("{agentResponse}", agentResponse)
            .Replace("{previousAgentResponse}", previousAgentResponse)
            .Replace("{structuredTags}", structuredTagsText)
            .Replace("{missingTags}", missingTagsText)
            .Replace("{pendingTags}", pendingTagsText);  

        var reply = await _chatService.ChatAsync(prompt);

        if (string.IsNullOrEmpty(reply))
        {
            throw new InvalidOperationException("C线摘要分析：LLM 返回空结果");
        }

        return ParseSummaryOutput(reply, structuredTags, missingTags);
    }

    private SummaryAnalysisResult ParseSummaryOutput(
        string rawOutput,
        StructuredTag[]? structuredTags = null,
        StructuredTag[]? missingTags = null)
    {
        var result = new SummaryAnalysisResult();

        var jsonStart = rawOutput.IndexOf('{');
        var jsonEnd = rawOutput.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
        {
            throw new InvalidOperationException($"C线摘要分析：无法从返回结果中提取 JSON: {rawOutput}");
        }

        var jsonContent = rawOutput.Substring(jsonStart, jsonEnd - jsonStart + 1);
        var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // ===== 解析摘要 =====
        if (root.TryGetProperty("summary", out var summary))
            result.Summary = summary.GetString() ?? "";

        if (root.TryGetProperty("overview", out var overview))
            result.Overview = overview.GetString() ?? "";

        // ===== 独立解析 missing_tag_definitions =====
        if (root.TryGetProperty("missing_tag_definitions", out var defs) && defs.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in defs.EnumerateObject())
            {
                result.MissingTagDefinitions[prop.Name] = prop.Value.GetString() ?? "";
            }
            Console.WriteLine($"[SummaryAnalyzer] 解析到 {result.MissingTagDefinitions.Count} 个缺失标签定义");
        }

        // ===== 解析用户偏好 =====
        if (root.TryGetProperty("preferences", out var prefs) && prefs.ValueKind == JsonValueKind.Array)
        {
            var prefList = new List<PreferenceEntry>();
            foreach (var item in prefs.EnumerateArray())
            {
                var key = item.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                var value = item.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                var confidence = item.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.5;

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || confidence < 0.4)
                {
                    Console.WriteLine($"[SummaryAnalyzer] 跳过低置信度偏好: {key}={value} ({confidence:F0%})");
                    continue;
                }

                // 解析 scene（支持字符串或数组）
                string scene = "";
                if (item.TryGetProperty("scene", out var sceneElem))
                {
                    if (sceneElem.ValueKind == JsonValueKind.String)
                    {
                        scene = sceneElem.GetString() ?? "";
                    }
                    else if (sceneElem.ValueKind == JsonValueKind.Array)
                    {
                        var scenes = new List<string>();
                        foreach (var elem in sceneElem.EnumerateArray())
                        {
                            if (elem.ValueKind == JsonValueKind.String)
                            {
                                var s = elem.GetString();
                                if (!string.IsNullOrEmpty(s))
                                    scenes.Add(s);
                            }
                        }
                        scene = string.Join(",", scenes);
                    }
                }

                prefList.Add(new PreferenceEntry
                {
                    Key = key,
                    Value = value,
                    Confidence = confidence,
                    Scene = scene
                });
            }

            var deduped = new Dictionary<string, PreferenceEntry>();
            foreach (var p in prefList)
            {
                deduped[p.Key] = p;
            }
            result.Preferences = deduped.Values.ToList();

            Console.WriteLine($"[SummaryAnalyzer] 提取到 {result.Preferences.Count} 条用户偏好");
        }

        // ===== 解析演化操作 =====
        if (root.TryGetProperty("evolution_actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            var actionList = new List<EvolutionAction>();
            foreach (var item in actions.EnumerateArray())
            {
                var action = new EvolutionAction
                {
                    Action = item.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "",
                    Code = item.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "",
                    TargetCode = item.TryGetProperty("target_code", out var tc) ? tc.GetString() : null,
                    Reason = item.TryGetProperty("reason", out var r) ? r.GetString() : null
                };
                if (!string.IsNullOrEmpty(action.Action) && !string.IsNullOrEmpty(action.Code))
                {
                    actionList.Add(action);
                }
            }
            result.EvolutionActions = actionList.ToArray();
        }

        // ===== 解析反馈判断 =====
        if (root.TryGetProperty("feedback", out var feedback) && feedback.ValueKind == JsonValueKind.Object)
        {
            var status = feedback.TryGetProperty("status", out var s) ? s.GetString() ?? "ignored" : "ignored";
            var reason = feedback.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";

            result.Feedback = new FeedbackEntry
            {
                Status = status,
                Reason = reason
            };
            Console.WriteLine($"[SummaryAnalyzer] 反馈: {status} - {reason}");
        }

        // ===== 解析 scene  =====
        if (root.TryGetProperty("scene", out var sceneObj) && sceneObj.ValueKind == JsonValueKind.Object)
        {
            var category = sceneObj.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "";
            var sub = sceneObj.TryGetProperty("sub", out var s) ? s.GetString() ?? "" : "";

            if (!string.IsNullOrEmpty(category))
            {
                result.SceneCategory = category;
                result.SceneSub = sub;
                Console.WriteLine($"[SummaryAnalyzer] 场景定稿: {category}/{sub}");
            }
        }

        // ===== ★ 解析标签合并决策 =====
        if (root.TryGetProperty("pending_tags_decision", out var pendingDecision) && pendingDecision.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in pendingDecision.EnumerateObject())
            {
                result.PendingTagsDecision[prop.Name] = prop.Value.GetString() ?? "";
            }
            Console.WriteLine($"[SummaryAnalyzer] 解析到 {result.PendingTagsDecision.Count} 个标签合并决策");
        }

        // ===== 填充标签 =====
        if (structuredTags != null && structuredTags.Length > 0)
        {
            result.Tags = structuredTags.Select(t => t.Tag).ToArray();
            result.StructuredTags = structuredTags;
        }
        else
        {
            result.Tags = Array.Empty<string>();
            result.StructuredTags = Array.Empty<StructuredTag>();
        }

        return result;
    }
}
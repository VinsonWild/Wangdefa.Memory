// ================================================================
// Middleware.cs — 中间件：特征推演 + 分流取数 + 组合
// ================================================================

using System.Text.Json;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.FeatureEngine;

public class Middleware
{
    private readonly IWangdefaMemory _memory;

    public Middleware(IWangdefaMemory memory)
    {
        _memory = memory;
    }

    public async Task<(string enrichedInput, CognitiveMatchResultModel? cognitiveResult, StructuredTag[] missingTags, string? frameId)> ProcessAsync(
        string input,
        string sessionId,
        IntentAnalysisResult intentResult)
    {
        var structuredTags = intentResult.StructuredTags;
        var hitCodes = new List<string>();
        var missingTags = new List<StructuredTag>();
        var processedTags = new HashSet<string>();

        // ============================================================
        // 1. A线 标签匹配 + 近义匹配 + 增量写入
        // ============================================================
        foreach (var st in structuredTags)
        {
            if (processedTags.Contains(st.Tag))
                continue;
            processedTags.Add(st.Tag);

            bool matched = false;
            string? matchedCode = null;

            // 1.1 精准匹配 + 子串匹配
            var code = _memory.GetTagCode(st.Tag, st.Dimension);

            if (code == null && st.Definitions != null && st.Definitions.Length > 0)
            {
                code = _memory.GetTagCodeByTagAndDefinitions(st.Tag, st.Definitions, st.Dimension);
            }

            if (code != null)
            {
                matchedCode = code;
                matched = true;
                Console.WriteLine($"✅ 精准命中: {st.Tag} → {code}");

                var entry = _memory.GetTagEntryByCode(code);
                if (entry != null)
                {
                    var synonymsJson = entry.Synonyms;
                    if (!string.IsNullOrEmpty(synonymsJson) && synonymsJson != "[]")
                    {
                        try
                        {
                            var storedSynonyms = JsonSerializer.Deserialize<string[]>(synonymsJson);
                            if (storedSynonyms != null && storedSynonyms.Length > 0)
                            {
                                foreach (var syn in storedSynonyms)
                                {
                                    if (string.IsNullOrEmpty(syn) || processedTags.Contains(syn))
                                        continue;
                                    processedTags.Add(syn);
                                    var synCode = _memory.GetTagCode(syn, st.Dimension);
                                    if (synCode != null && !hitCodes.Contains(synCode))
                                    {
                                        hitCodes.Add(synCode);
                                        Console.WriteLine($"🔗 近义扩展（标签池）: {syn} → {synCode}");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // 解析失败，忽略
                        }
                    }
                }
            }

            // 1.2 近义匹配
            if (!matched && st.Synonyms != null && st.Synonyms.Length > 0)
            {
                foreach (var syn in st.Synonyms)
                {
                    if (string.IsNullOrEmpty(syn) || processedTags.Contains(syn))
                        continue;
                    processedTags.Add(syn);

                    var synCode = _memory.GetTagCode(syn, st.Dimension);
                    if (synCode != null)
                    {
                        matchedCode = synCode;
                        matched = true;
                        Console.WriteLine($"✅ 近义命中: {syn} → {synCode}");
                        break;
                    }
                }
            }

            if (matched && matchedCode != null)
            {
                hitCodes.Add(matchedCode);
                continue;
            }

            // 1.3 未命中 → 新增标签
            if (!matched)
            {
                var definitionStr = st.Definitions != null && st.Definitions.Length > 0
                    ? string.Join(", ", st.Definitions)
                    : "";

                _memory.AddTagWithSynonyms(st.Tag, st.Dimension, definitionStr, st.Synonyms);

                var newCode = _memory.GetTagCode(st.Tag, st.Dimension);
                if (newCode != null)
                {
                    hitCodes.Add(newCode);
                    Console.WriteLine($"🆕 新增标签并命中: {st.Tag} → {newCode}");
                }
                else
                {
                    missingTags.Add(st);
                    Console.WriteLine($"⚠️ 新增标签失败: {st.Tag}");
                }
            }
        }

        // ============================================================
        // 2. ★ 写卡片框架（C线前置）
        //    - 和检索并行，用的是同一批标签
        //    - 保证"存"和"查"的标签一致
        // ============================================================
        var topicId = sessionId;
        var tagTexts = structuredTags.Select(t => t.Tag).ToList();
        string? frameId = null;

        if (tagTexts.Count > 0)
        {
            try
            {
                frameId = await _memory.WriteMemoryFrame(
                    topicId: topicId,
                    userInput: input,
                    perception: intentResult.Perception,
                    tags: tagTexts,
                    route: intentResult.Route
                );
                Console.WriteLine($"[Middleware] ✅ C线框架已写入，cardId: {frameId}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"C线框架写入失败: {ex.Message}", ex);
            }
        }
        else
        {
            Console.WriteLine($"[Middleware] ⚠️ 无标签，跳过C线框架写入");
        }

        // ============================================================
        // 3. 用命中的 code 查密码簿 → 认知卡片
        // ============================================================
        CognitiveMatchResultModel? cognitiveResult = null;
        if (hitCodes.Count > 0)
        {
            try
            {
                cognitiveResult = await _memory.CognitiveMatchByCodes(hitCodes.Distinct().ToList(), sessionId);
                if (cognitiveResult != null)
                {
                    Console.WriteLine($"🧠 认知匹配命中: {cognitiveResult.Summary}");
                    Console.WriteLine($"   RecordId: {cognitiveResult.RecordId}");
                    Console.WriteLine($"   SourcePath: {cognitiveResult.SourcePath}");
                }
                else
                {
                    Console.WriteLine("🧠 认知匹配未命中（无历史卡片）");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"记忆匹配查询失败: {ex.Message}", ex);
            }
        }
        else
        {
            Console.WriteLine("🧠 无命中标签，跳过推演");
        }

        // ============================================================
        // 4. 分流取数（L5 传递层）
        // ============================================================
        var route = intentResult.Route;
        var deepContent = "";

        if (cognitiveResult != null && !string.IsNullOrEmpty(cognitiveResult.Summary))
        {
            switch (route)
            {
                case "medium":
                    if (!string.IsNullOrEmpty(cognitiveResult.SourcePath))
                    {
                        var overview = await _memory.GetOverview(cognitiveResult.SourcePath);
                        if (!string.IsNullOrEmpty(overview))
                        {
                            deepContent = overview;
                            Console.WriteLine($"📄 已读取概览: {cognitiveResult.SourcePath}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ 概览内容为空或文件不存在: {cognitiveResult.SourcePath}");
                        }
                    }
                    break;

                case "deep":
                    if (!string.IsNullOrEmpty(cognitiveResult.RecordId))
                    {
                        var fullText = await _memory.GetFullText(cognitiveResult.RecordId);
                        if (!string.IsNullOrEmpty(fullText))
                        {
                            deepContent = fullText;
                            Console.WriteLine($"📄 已读取原文: {cognitiveResult.RecordId}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ 原文内容为空或记录不存在: {cognitiveResult.RecordId}");
                        }
                    }
                    break;

                case "shallow":
                default:
                    Console.WriteLine($"📄 shallow 模式，不读取深层内容");
                    break;
            }
        }

        // ============================================================
        // 5. 组合 enrichedInput
        // ============================================================
        var parts = new List<string>();

        parts.Add("=== 当前用户消息 ===");
        parts.Add(input);
        parts.Add("");

        parts.Add("=== 意图分析 ===");
        var intentDesc = $"用户意图：{intentResult.Intent}，场景：{intentResult.Perception.Scene}，情绪：{intentResult.Perception.Emotion}";
        parts.Add(intentDesc);
        parts.Add($"上下文摘要：{intentResult.ContextSummary}");
        parts.Add("");

        if (cognitiveResult != null && !string.IsNullOrEmpty(cognitiveResult.Summary))
        {
            parts.Add("=== 相关记忆 ===");
            parts.Add($"摘要：{cognitiveResult.Summary}");
            if (cognitiveResult.ContentTags?.Length > 0)
                parts.Add($"标签：{string.Join(", ", cognitiveResult.ContentTags)}");
            if (cognitiveResult.Confidence > 0)
                parts.Add($"可信度：{(cognitiveResult.Confidence * 100):F0}%");

            if (cognitiveResult.Preferences != null && cognitiveResult.Preferences.Any())
            {
                parts.Add($"偏好：{string.Join(", ", cognitiveResult.Preferences.Select(p => $"{p.Key}={p.Value}({p.Confidence:F0%})"))}");
            }

            if (intentResult.MemoryInjectionMode == "detail" || intentResult.MemoryInjectionMode == "full")
            {
                if (!string.IsNullOrEmpty(cognitiveResult.SourcePath))
                    parts.Add($"概览路径：{cognitiveResult.SourcePath}");
                if (!string.IsNullOrEmpty(deepContent))
                    parts.Add($"详情：{deepContent}");
            }

            if (!string.IsNullOrEmpty(cognitiveResult.RecordId))
                parts.Add($"记录ID：{cognitiveResult.RecordId}");

            parts.Add("");
        }

        var enrichedInput = string.Join("\n", parts);

        return (enrichedInput, cognitiveResult, missingTags.ToArray(), frameId);
    }
}
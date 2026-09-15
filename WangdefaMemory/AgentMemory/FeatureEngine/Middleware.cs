// ================================================================
// Middleware.cs — 中间件：特征推演 + 分流取数 + 组合
// ================================================================

using System.Text.Json;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine;

public class Middleware
{
    private readonly IWangdefaMemory _memory;
    private readonly SceneStore _sceneStore;
    private readonly IThinkingStore _thinkingStore;

    public Middleware(IWangdefaMemory memory, SceneStore sceneStore, IThinkingStore thinkingStore)
    {
        _memory = memory;
        _sceneStore = sceneStore;
        _thinkingStore = thinkingStore;
    }

    public async Task<(string enrichedInput, CognitiveMatchResultModel? cognitiveResult, StructuredTag[] missingTags, string? frameId, List<TagEntry> malformedTags)> ProcessAsync(
        string input,
        string sessionId,
        IntentAnalysisResult intentResult)
    {
        var structuredTags = intentResult.StructuredTags;
        var hitCodes = new List<string>();
        var missingTags = new List<StructuredTag>();
        var processedTags = new HashSet<string>();
        var malformedTags = new List<TagEntry>();

        // ============================================================
        // 1. A线 标签匹配 + 近义匹配 + 关联扩展（只读，不写入）
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
                    // 近义扩展（标签池）
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

                    // ★ 关联扩展（synonym 多跳 / related 1 跳 / loose 跳过）
                    ExpandRelations(code, hitCodes);
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

                // ★ 批次 E：统一出口检测残缺（覆盖分支 A 和分支 B）
                var matchedEntry = _memory.GetTagEntryByCode(matchedCode);
                if (matchedEntry != null && _memory.IsMalformed(matchedEntry))
                {
                    if (malformedTags.All(t => t.Code != matchedEntry.Code))
                    {
                        malformedTags.Add(matchedEntry);
                        Console.WriteLine($"[Middleware] ⚠️ 残缺标签待对齐: {matchedEntry.Tag}");
                    }
                }

                continue;
            }

            // 1.3 未命中 → 只读，不新增（留给 C 线处理）
            if (!matched)
            {
                Console.WriteLine($"⏳ 标签未命中，留给 C 线处理: {st.Tag}");
                missingTags.Add(st);
            }
        }

        // ============================================================
        // 2. ★ A 线查场景库命中层
        // ============================================================
        var sceneCategory = intentResult.Perception.Scene;
        var sceneSub = intentResult.Perception.SceneSub;
        bool sceneKnown = false;

        if (!string.IsNullOrEmpty(sceneCategory))
        {
            var existingScenes = _sceneStore.GetByCategory(sceneCategory);
            if (!string.IsNullOrEmpty(sceneSub))
            {
                sceneKnown = existingScenes.Any(s => s.Sub == sceneSub);
            }
            else
            {
                sceneKnown = existingScenes.Any();
            }

            if (sceneKnown)
            {
                Console.WriteLine($"[Middleware] 场景命中: {sceneCategory}/{sceneSub}");
            }
            else
            {
                Console.WriteLine($"[Middleware] 场景未命中，留给 C 线收录: {sceneCategory}/{sceneSub}");
            }
        }

        // ============================================================
        // 3. ★ 写卡片框架（C线前置）
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
        // 4. 用命中的 code 查密码簿 → 认知卡片
        // ============================================================
        CognitiveMatchResultModel? cognitiveResult = null;
        if (hitCodes.Count > 0)
        {
            try
            {
                cognitiveResult = await _memory.CognitiveMatchByCodes(
                    hitCodes.Distinct().ToList(),
                    sessionId,
                    intentResult.Perception.Scene,
                    intentResult.Perception.SceneSub);
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
        // 5. 分流取数（L5 传递层）
        // ============================================================
        var route = intentResult.Route;
        var deepContent = "";

        if (cognitiveResult != null && !string.IsNullOrEmpty(cognitiveResult.Summary))
        {
            switch (route)
            {
                case "medium":
                    // ★ 改用 OverviewPointer
                    if (!string.IsNullOrEmpty(cognitiveResult.OverviewPointer))
                    {
                        var overview = await _memory.GetOverview(cognitiveResult.OverviewPointer);
                        if (!string.IsNullOrEmpty(overview))
                        {
                            deepContent = overview;
                            Console.WriteLine($"📄 已读取概览: {cognitiveResult.OverviewPointer}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ 概览内容为空或文件不存在: {cognitiveResult.OverviewPointer}");
                        }
                    }
                    else if (!string.IsNullOrEmpty(cognitiveResult.SourcePath))
                    {
                        // 兜底：没有 OverviewPointer 时尝试用 SourcePath
                        var overview = await _memory.GetOverview(cognitiveResult.SourcePath);
                        if (!string.IsNullOrEmpty(overview))
                        {
                            deepContent = overview;
                            Console.WriteLine($"📄 已读取概览（兜底）: {cognitiveResult.SourcePath}");
                        }
                    }
                    break;

                case "deep":
                    // ★ 优先用 EventId 读完整事件
                    if (!string.IsNullOrEmpty(cognitiveResult.EventId))
                    {
                        try
                        {
                            var eventModel = await _thinkingStore.LoadEvent(cognitiveResult.EventId);
                            if (eventModel != null && !string.IsNullOrEmpty(eventModel.Data?.AgentResponse))
                            {
                                deepContent = eventModel.Data.AgentResponse;
                                Console.WriteLine($"📄 已读取完整事件: {cognitiveResult.EventId}");
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ 完整事件不存在或内容为空: {cognitiveResult.EventId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ 读取完整事件失败: {ex.Message}");
                        }
                    }
                    // ★ 兜底：没有 EventId 时尝试用 RecordId
                    else if (!string.IsNullOrEmpty(cognitiveResult.RecordId))
                    {
                        var fullText = await _memory.GetFullText(cognitiveResult.RecordId);
                        if (!string.IsNullOrEmpty(fullText))
                        {
                            deepContent = fullText;
                            Console.WriteLine($"📄 已读取原文（兜底）: {cognitiveResult.RecordId}");
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
        // 6. 组合 enrichedInput
        // ============================================================
        var parts = new List<string>();

        parts.Add("=== 当前用户消息 ===");
        parts.Add(input);
        parts.Add("");

        parts.Add("=== 意图分析 ===");
        var intentDesc = $"用户意图：{intentResult.Intent}，场景：{intentResult.Perception.Scene}，情绪：{intentResult.Perception.Emotion}";
        parts.Add(intentDesc);
        parts.Add($"上下文摘要：{intentResult.ContextSummary}");

        var styleConstraint = GetStyleConstraint(intentResult.ResponseStyle);
        if (!string.IsNullOrEmpty(styleConstraint))
        {
            parts.Add(styleConstraint);
        }
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
                var currentScene = intentResult.Perception.Scene;
                var filtered = cognitiveResult.Preferences
                    .Where(p => p.Key != "反馈")
                    .Where(p => string.IsNullOrEmpty(p.Scene) || p.Scene.Contains(currentScene))
                    .ToList();
                if (filtered.Any())
                {
                    parts.Add($"偏好：{string.Join(", ", filtered.Select(p => $"{p.Key}={p.Value}({p.Confidence:F0%})"))}");
                }
            }

            // ★ 放宽拼装闸门：route 为 deep 时强制拼 deepContent
            if (intentResult.MemoryInjectionMode == "detail" ||
                intentResult.MemoryInjectionMode == "full" ||
                route == "deep")
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

        return (enrichedInput, cognitiveResult, missingTags.ToArray(), frameId, malformedTags);
    }

    /// <summary>
    /// 关联扩展（消费层）
    /// synonym → 多跳（visited 防环，上限 3）
    /// related → 仅 1 跳，入 hitCodes
    /// loose   → 跳过
    /// A 线只读，不写入
    /// </summary>
    private void ExpandRelations(string code, List<string> hitCodes)
    {
        var relations = _memory.GetRelations(code);
        if (relations == null || relations.Count == 0) return;

        var visited = new HashSet<string> { code };
        var synonymQueue = new Queue<string>();

        // 第一跳：处理当前 code 的所有关联
        foreach (var rel in relations)
        {
            if (string.IsNullOrEmpty(rel.Code)) continue;

            switch (rel.Level)
            {
                case RelationLevel.Synonym:
                    if (visited.Add(rel.Code))
                    {
                        synonymQueue.Enqueue(rel.Code);
                        AddToHitCodes(rel.Code, hitCodes);
                        Console.WriteLine($"🔗 同义关联: {code} → {rel.Code}");
                    }
                    break;

                case RelationLevel.Related:
                    // 仅 1 跳，不继续扩展
                    if (visited.Add(rel.Code))
                    {
                        AddToHitCodes(rel.Code, hitCodes);
                        Console.WriteLine($"🔗 相关关联: {code} → {rel.Code}");
                    }
                    break;

                case RelationLevel.Loose:
                    // 跳过，不参与检索
                    break;
            }
        }

        // synonym 多跳（上限 3 跳）
        var depth = 1;
        while (synonymQueue.Count > 0 && depth < 3)
        {
            var current = synonymQueue.Dequeue();
            var nextRelations = _memory.GetRelations(current);
            if (nextRelations == null) continue;

            foreach (var rel in nextRelations)
            {
                if (rel.Level != RelationLevel.Synonym) continue;
                if (string.IsNullOrEmpty(rel.Code)) continue;
                if (!visited.Add(rel.Code)) continue;

                synonymQueue.Enqueue(rel.Code);
                AddToHitCodes(rel.Code, hitCodes);
                Console.WriteLine($"🔗 同义多跳: {code} → ... → {rel.Code} (depth={depth + 1})");
            }
            depth++;
        }
    }

    private void AddToHitCodes(string code, List<string> hitCodes)
    {
        if (!hitCodes.Contains(code))
            hitCodes.Add(code);
    }

    /// <summary>
    /// 根据回复风格生成字数约束指令
    /// </summary>
    private string GetStyleConstraint(string responseStyle)
    {
        return responseStyle switch
        {
            "concise" => "=== 回复约束 ===\n回复控制在 100 字内，直接回应用户，不展开背景，不追问。",
            "balanced" => "=== 回复约束 ===\n回复控制在 300 字内，可适度展开关键信息，有必要可追问一次。",
            "detailed" => "=== 回复约束 ===\n回复不限制，充分展开分析，可多轮追问，可提供备选方案。",
            "executive" => "=== 回复约束 ===\n回复不限制，必须给出明确结论或建议，结构清晰，不加无关信息。",
            _ => ""
        };
    }
}
// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using Wangdefa.AgentMemory.FeatureEngine.Models;


namespace Wangdefa.AgentMemory.FeatureEngine.TagServices;

/// <summary>
/// 标签演化服务 - 合并/分裂/弃用/激活/关联
/// </summary>
public class TagEvolutionService
{
    private readonly TagStore _store;
    private readonly TagCache _cache;
    private PasswordBook? _passwordBook;

    /// <summary>
    /// 释义分隔符（兼容三种历史写法：半角逗号 / 全角逗号 / 顿号）
    /// 实测：半角逗号 82.8%、全角逗号 11.8%、顿号 5.5%
    /// </summary>
    private static readonly char[] DefinitionSeparators = { ',', '，', '、' };

    public TagEvolutionService(TagStore store, TagCache cache)
    {
        _store = store;
        _cache = cache;
    }

    public void SetPasswordBook(PasswordBook passwordBook)
    {
        _passwordBook = passwordBook;
    }

    public void Deprecate(string code, string? reason = null)
    {
        var entry = _cache.GetByCode(code) ?? _store.LoadFromDbByCode(code);
        if (entry == null) return;

        entry.Status = "deprecated";
        if (!string.IsNullOrEmpty(reason))
            Console.WriteLine($"[TagEvolutionService] 弃用原因: {code} - {reason}");

        _store.UpdateStatus(code, "deprecated");
        _cache.Update(entry);
        Console.WriteLine($"[TagEvolutionService] 已弃用: {code}");
    }

    public void MergeTags(string sourceCode, string targetCode)
    {
        var sourceEntry = _cache.GetByCode(sourceCode) ?? _store.LoadFromDbByCode(sourceCode);
        var targetEntry = _cache.GetByCode(targetCode) ?? _store.LoadFromDbByCode(targetCode);

        if (sourceEntry == null || targetEntry == null) return;
        if (_passwordBook == null)
        {
            Console.WriteLine("[TagEvolutionService] PasswordBook 未设置，无法执行合并");
            return;
        }

        var cards = _passwordBook.GetCards(sourceCode);
        if (cards.Count == 0)
        {
            // 无卡片迁移路径：仍要做语义搬运
            MigrateSemantics(sourceEntry, targetEntry);

            sourceEntry.Status = "merged";
            sourceEntry.MergedTo = targetCode;
            _store.UpdateStatus(sourceCode, "merged", targetCode);
            _cache.Update(sourceEntry);
            Console.WriteLine($"[TagEvolutionService] 已合并（无卡片迁移）: {sourceCode} → {targetCode}");
            return;
        }

        _passwordBook.MoveCards(sourceCode, targetCode);

        // 语义搬运（近义词 / 释义 / 关联）
        MigrateSemantics(sourceEntry, targetEntry);

        sourceEntry.Status = "merged";
        sourceEntry.MergedTo = targetCode;
        _store.UpdateStatus(sourceCode, "merged", targetCode);
        _cache.Update(sourceEntry);

        Console.WriteLine($"[TagEvolutionService] 已合并: {sourceCode} → {targetCode}, {cards.Count} 张卡片");
    }

    /// <summary>
    /// 合并时把源标签的语义资产搬运到目标标签
    /// 三项并集：近义词 / 释义 / 关联（含重定向 + 去重 + 双向一致性）
    /// </summary>
    private void MigrateSemantics(TagEntry source, TagEntry target)
    {
        // ===== 1. 近义词并集 =====
        var sourceSynonyms = ParseStringArray(source.Synonyms);
        var targetSynonyms = ParseStringArray(target.Synonyms);
        var mergedSynonyms = targetSynonyms.Union(sourceSynonyms).Distinct().ToList();
        if (mergedSynonyms.Count != targetSynonyms.Count)
        {
            var synonymsJson = JsonSerializer.Serialize(mergedSynonyms);
            target.Synonyms = synonymsJson;
            _store.UpdateSynonyms(target.Code, synonymsJson);
            Console.WriteLine($"[TagEvolutionService] 近义词已并入: {source.Code} → {target.Code} ({sourceSynonyms.Count} 项)");
        }

        // ===== 2. 释义并集 =====
        if (!string.IsNullOrWhiteSpace(source.Definition))
        {
            var mergedDefinition = CombineDefinitions(target.Definition, source.Definition);
            if (mergedDefinition != target.Definition)
            {
                target.Definition = mergedDefinition;
                _store.UpdateDefinition(target.Code, mergedDefinition);
                Console.WriteLine($"[TagEvolutionService] 释义已并入: {source.Code} → {target.Code}");
            }
        }

        // ===== 3. 关联重定向（一层扫描 + 去重 + 双向一致性） =====
        MigrateRelations(source, target);

        // 刷缓存（MigrateRelations 内部可能改了 target.RelatedCodes）
        _cache.Update(target);
    }

    /// <summary>
    /// 更新维度（批次 E 对齐用）
    /// </summary>
    public void UpdateDimensions(string code, List<string> dimensions)
    {
        if (string.IsNullOrEmpty(code) || dimensions == null) return;

        var entry = _cache.GetByCode(code) ?? _store.LoadFromDbByCode(code);
        if (entry == null) return;

        var json = JsonSerializer.Serialize(dimensions);
        entry.Dimensions = json;
        _store.UpdateDimensions(code, json);
        _cache.Update(entry);

        Console.WriteLine($"[TagEvolutionService] 维度已更新: {code} → {json}");
    }

    /// <summary>
    /// 释义合并（纯函数）：目标空则继承；非空则追加（去重，分隔符兼容三种）
    /// 拆分覆盖 `,` / `，` / `、`；输出统一用半角逗号 + 空格
    /// </summary>
    private string CombineDefinitions(string targetDef, string sourceDef)
    {
        if (string.IsNullOrWhiteSpace(targetDef)) return sourceDef;
        if (string.IsNullOrWhiteSpace(sourceDef)) return targetDef;

        // 拆出已有片段（统一 trim 后比对，忽略分隔符差异）
        var existing = targetDef
            .Split(DefinitionSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToHashSet();

        var additions = sourceDef
            .Split(DefinitionSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && !existing.Contains(s))
            .ToList();

        if (additions.Count == 0) return targetDef;

        // 追加：先规整末尾分隔符，再用半角逗号 + 空格拼接
        var trimmed = targetDef.TrimEnd(DefinitionSeparators).TrimEnd();
        return trimmed + ", " + string.Join(", ", additions);
    }

    /// <summary>
    /// 追加释义到已存在的标签（落库）
    /// 拆分现有释义，新释义逐个判断：已存在 → 跳过；不存在 → 追加
    /// 有变化才写回
    /// </summary>
    public void MergeDefinitions(string code, string newDefinition)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrWhiteSpace(newDefinition)) return;

        var entry = _cache.GetByCode(code) ?? _store.LoadFromDbByCode(code);
        if (entry == null) return;

        var merged = CombineDefinitions(entry.Definition, newDefinition);
        if (merged == entry.Definition) return;   // 无变化

        entry.Definition = merged;
        _store.UpdateDefinition(code, merged);
        _cache.Update(entry);

        Console.WriteLine($"[TagEvolutionService] 释义已累积: {code} → {merged}");
    }

    /// <summary>
    /// 关联重定向：
    /// 1. 一层扫描：所有 RelatedCodes 中含 source 的标签，把 source 替换为 target
    /// 2. 去重：同一目标 code 只留一个
    /// 3. 双向一致性：source 的关联并入 target，target 的关联补齐反向
    /// </summary>
    private void MigrateRelations(TagEntry source, TagEntry target)
    {
        // --- 3.1 source 自身关联并入 target ---
        var sourceRelations = ParseRelations(source.RelatedCodes);
        var targetRelations = ParseRelations(target.RelatedCodes);
        var targetCodes = targetRelations.Select(r => r.Code).ToHashSet();

        var newTargetRelations = new List<TagRelation>(targetRelations);
        foreach (var rel in sourceRelations)
        {
            if (string.IsNullOrEmpty(rel.Code)) continue;
            if (rel.Code == target.Code) continue;   // 跳过自关联
            if (targetCodes.Contains(rel.Code)) continue;
            newTargetRelations.Add(rel);
            targetCodes.Add(rel.Code);
        }

        // 写入 target
        if (newTargetRelations.Count != targetRelations.Count)
        {
            var json = JsonSerializer.Serialize(newTargetRelations);
            target.RelatedCodes = json;
            _store.UpdateRelatedCodes(target.Code, json);
        }

        // --- 3.2 反向补齐：source 原来指向谁，谁就应该指向 target ---
        foreach (var rel in sourceRelations)
        {
            if (string.IsNullOrEmpty(rel.Code)) continue;
            if (rel.Code == target.Code) continue;

            var otherEntry = _cache.GetByCode(rel.Code) ?? _store.LoadFromDbByCode(rel.Code);
            if (otherEntry == null) continue;

            var otherRelations = ParseRelations(otherEntry.RelatedCodes);
            var otherCodes = otherRelations.Select(r => r.Code).ToHashSet();

            // 移除指向 source 的条目（如果存在），加入指向 target 的条目
            bool changed = otherRelations.RemoveAll(r => r.Code == source.Code) > 0;

            if (!otherCodes.Contains(target.Code))
            {
                otherRelations.Add(new TagRelation { Code = target.Code, Level = rel.Level });
                changed = true;
            }

            if (changed)
            {
                var json = JsonSerializer.Serialize(otherRelations);
                otherEntry.RelatedCodes = json;
                _store.UpdateRelatedCodes(otherEntry.Code, json);
                _cache.Update(otherEntry);
            }
        }

        // --- 3.3 source 自身保留一条指向 target 的记录 ---
        var sourceSelfRelations = ParseRelations(source.RelatedCodes);
        sourceSelfRelations.RemoveAll(r => r.Code == source.Code);   // 清自关联（如果有）
        if (!sourceSelfRelations.Any(r => r.Code == target.Code))
        {
            sourceSelfRelations.Add(new TagRelation { Code = target.Code, Level = RelationLevel.Related });
        }
        var sourceJson = JsonSerializer.Serialize(sourceSelfRelations);
        source.RelatedCodes = sourceJson;
        _store.UpdateRelatedCodes(source.Code, sourceJson);
    }

    /// <summary>
    /// 解析 JSON 字符串数组（用于 Synonyms）
    /// </summary>
    private List<string> ParseStringArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public void SplitTag(string sourceCode, string targetCode, string? reason = null)
    {
        var sourceEntry = _cache.GetByCode(sourceCode) ?? _store.LoadFromDbByCode(sourceCode);
        if (sourceEntry == null) return;
        if (_passwordBook == null)
        {
            Console.WriteLine("[TagEvolutionService] PasswordBook 未设置，无法执行分裂");
            return;
        }

        var targetEntry = _cache.GetByCode(targetCode) ?? _store.LoadFromDbByCode(targetCode);
        if (targetEntry == null)
        {
            // 需要在外部创建 target 标签，这里只记录
            Console.WriteLine($"[TagEvolutionService] 目标标签不存在: {targetCode}，请先创建");
            return;
        }

        var cards = _passwordBook.GetCards(sourceCode);
        if (cards.Count == 0)
        {
            sourceEntry.Status = "deprecated";
            if (!string.IsNullOrEmpty(reason))
                sourceEntry.Definition = reason;
            _store.UpdateStatus(sourceCode, "deprecated");
            _cache.Update(sourceEntry);
            Console.WriteLine($"[TagEvolutionService] 已分裂（无卡片迁移）: {sourceCode} → {targetCode}");
            return;
        }

        _passwordBook.MoveCards(sourceCode, targetCode);

        sourceEntry.Status = "deprecated";
        if (!string.IsNullOrEmpty(reason))
            sourceEntry.Definition = reason;
        _store.UpdateStatus(sourceCode, "deprecated");
        _cache.Update(sourceEntry);

        Console.WriteLine($"[TagEvolutionService] 已分裂: {sourceCode} → {targetCode}, {cards.Count} 张卡片");
    }

    public void Activate(string code)
    {
        var entry = _cache.GetByCode(code) ?? _store.LoadFromDbByCode(code);
        if (entry == null) return;

        entry.Status = "active";
        _store.UpdateStatus(code, "active");
        _cache.Update(entry);

        Console.WriteLine($"[TagEvolutionService] 标签已激活: {entry.Tag} → {code}");
    }

    // ============================================================
    // 关联读写
    // ============================================================

    /// <summary>
    /// 读取标签关联列表（兼容新旧格式）
    /// 旧格式 ["TAG_001"] → 转 { Code, Level=related }
    /// 新格式 [{"code":"","level":""}] → 直接解析
    /// </summary>
    public List<TagRelation> GetRelations(string code)
    {
        var entry = _cache.GetByCode(code) ?? _store.LoadFromDbByCode(code);
        if (entry == null) return new List<TagRelation>();
        return ParseRelations(entry.RelatedCodes);
    }

    /// <summary>
    /// 建立双向关联（synonym / related / loose）
    /// 边界：code == targetCode 跳过；目标必须已存在；绝不新增标签行
    /// </summary>
    public void AddRelation(string code, string targetCode, string level)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(targetCode)) return;
        if (code == targetCode) return;

        var sourceEntry = _cache.GetByCode(code) ?? _store.LoadFromDbByCode(code);
        if (sourceEntry == null)
        {
            Console.WriteLine($"[TagEvolutionService] ⚠️ 源标签不存在，跳过关联: {code}");
            return;
        }

        var targetEntry = _cache.GetByCode(targetCode) ?? _store.LoadFromDbByCode(targetCode);
        if (targetEntry == null)
        {
            // 关键边界：目标不存在，绝不新增标签行
            Console.WriteLine($"[TagEvolutionService] ⚠️ 目标标签不存在，跳过关联: {targetCode}");
            return;
        }

        // 双向写入
        ApplyRelation(sourceEntry, targetCode, level);
        ApplyRelation(targetEntry, code, level);

        Console.WriteLine($"[TagEvolutionService] 建立关联: {code} ↔ {targetCode} ({level})");
    }

    /// <summary>
    /// 对单个标签应用关联（内部方法，不检查目标存在性）
    /// 已存在同 code → 允许升级，不允许降级
    /// </summary>
    private void ApplyRelation(TagEntry entry, string targetCode, string level)
    {
        var relations = ParseRelations(entry.RelatedCodes);
        var existing = relations.FirstOrDefault(r => r.Code == targetCode);

        if (existing != null)
        {
            if (!CanUpgradeLevel(existing.Level, level))
                return;  // 无变化
            existing.Level = level;
            Console.WriteLine($"[TagEvolutionService] 关联等级升级: {entry.Code} → {targetCode}, {level}");
        }
        else
        {
            relations.Add(new TagRelation { Code = targetCode, Level = level });
        }

        var json = JsonSerializer.Serialize(relations);
        entry.RelatedCodes = json;
        _store.UpdateRelatedCodes(entry.Code, json);
        _cache.Update(entry);
    }

    /// <summary>
    /// 等级权重比较：synonym > related > loose
    /// </summary>
    private bool CanUpgradeLevel(string current, string incoming)
    {
        var weights = new Dictionary<string, int>
        {
            [RelationLevel.Loose] = 0,
            [RelationLevel.Related] = 1,
            [RelationLevel.Synonym] = 2
        };
        return weights.GetValueOrDefault(incoming, 0) > weights.GetValueOrDefault(current, 0);
    }

    /// <summary>
    /// 解析 RelatedCodes JSON，兼容新旧格式
    /// </summary>
    private List<TagRelation> ParseRelations(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<TagRelation>();

        // 先试新格式
        try
        {
            var newFormat = JsonSerializer.Deserialize<List<TagRelation>>(json);
            if (newFormat != null && newFormat.Count > 0 && newFormat.All(r => r != null && !string.IsNullOrEmpty(r.Code)))
                return newFormat;
        }
        catch { /* 落到旧格式 */ }

        // 再试旧格式
        try
        {
            var oldFormat = JsonSerializer.Deserialize<List<string>>(json);
            if (oldFormat != null)
                return oldFormat
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Select(c => new TagRelation { Code = c, Level = RelationLevel.Related })
                    .ToList();
        }
        catch { }

        return new List<TagRelation>();
    }
}
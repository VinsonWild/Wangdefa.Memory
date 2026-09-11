// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine.TagServices;

/// <summary>
/// 标签演化服务 - 合并/分裂/弃用/激活
/// </summary>
public class TagEvolutionService
{
    private readonly TagStore _store;
    private readonly TagCache _cache;
    private PasswordBook? _passwordBook;

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
            entry.Definition = reason;

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
            sourceEntry.Status = "merged";
            sourceEntry.MergedTo = targetCode;
            _store.UpdateStatus(sourceCode, "merged", targetCode);
            _cache.Update(sourceEntry);
            Console.WriteLine($"[TagEvolutionService] 已合并（无卡片迁移）: {sourceCode} → {targetCode}");
            return;
        }

        _passwordBook.MoveCards(sourceCode, targetCode);

        sourceEntry.Status = "merged";
        sourceEntry.MergedTo = targetCode;
        _store.UpdateStatus(sourceCode, "merged", targetCode);
        _cache.Update(sourceEntry);

        Console.WriteLine($"[TagEvolutionService] 已合并: {sourceCode} → {targetCode}, {cards.Count} 张卡片");
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
}
// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine.TagServices;

/// <summary>
/// 标签缓存管理 - 内存缓存
/// </summary>
public class TagCache
{
    private readonly Dictionary<string, TagEntry> _tagCache;
    private readonly Dictionary<string, TagEntry> _codeCache;
    private int _nextSeq;
    private bool _isFullyLoaded;

    public TagCache()
    {
        _tagCache = new Dictionary<string, TagEntry>();
        _codeCache = new Dictionary<string, TagEntry>();
        _isFullyLoaded = false;
        _nextSeq = 0;
    }

    public bool IsFullyLoaded => _isFullyLoaded;

    public TagEntry? GetByTag(string tag)
    {
        return _tagCache.TryGetValue(tag, out var entry) ? entry : null;
    }

    public TagEntry? GetByCode(string code)
    {
        return _codeCache.TryGetValue(code, out var entry) ? entry : null;
    }

    /// <summary>
    /// 是否允许进缓存。
    /// 只有 deprecated 被排除 —— 它已彻底退场，无任何指向字段，不该被召回。
    ///
    /// ★ merged 有意保留：MergedTo 的存在就是为了兼容旧标签名，它是「活路标」，
    ///   留缓存可让 ResolveCode 一跳到位。若将来要剔除 merged，
    ///   需同步评估 B-1 重定向链路（当前该链路冷启动走 DB 亦可通，但会多一次查询）。
    ///
    /// 此判据与 TagStore 的 SQL 过滤（status != 'deprecated'）保持一致。
    /// </summary>
    private static bool IsCacheable(TagEntry entry)
        => entry.Status != "deprecated";

    public void Add(TagEntry entry)
    {
        if (!IsCacheable(entry)) return;
        _tagCache[entry.Tag] = entry;
        _codeCache[entry.Code] = entry;
        var seq = ExtractSeq(entry.Code);
        if (seq > _nextSeq) _nextSeq = seq;
    }

    public void Update(TagEntry entry)
    {
        // 状态变为 deprecated → 从缓存剔除（Deprecate 走的就是这条路径）
        if (!IsCacheable(entry))
        {
            Remove(entry.Tag, entry.Code);
            return;
        }
        _tagCache[entry.Tag] = entry;
        _codeCache[entry.Code] = entry;
    }

    public void Remove(string tag, string code)
    {
        var removedByTag = _tagCache.Remove(tag);
        var removedByCode = _codeCache.Remove(code);

        // 半清检测：两个索引不一致时告警（当前不会触发，tag 不会改名）
        if (removedByTag != removedByCode)
        {
            Console.WriteLine(
                $"[TagCache] ⚠️ 缓存半清: tag='{tag}'(移除={removedByTag}), code='{code}'(移除={removedByCode})");
        }
    }

    public List<TagEntry> GetAllTags() => _tagCache.Values.ToList();

    public List<string> GetAllCodes() => _codeCache.Keys.ToList();

    public int NextSeq => _nextSeq + 1;

    public void SetFullyLoaded()
    {
        _isFullyLoaded = true;
    }

    public List<TagEntry> Search(string query)
    {
        var results = new List<TagEntry>();
        var lowerQuery = query.ToLower();

        foreach (var entry in _tagCache.Values)
        {
            if (entry.Tag.ToLower() == lowerQuery)
                results.Insert(0, entry);
            else if (entry.Tag.ToLower().Contains(lowerQuery))
                results.Add(entry);
        }

        return results;
    }

    private int ExtractSeq(string code)
    {
        var parts = code.Split('_');
        if (parts.Length == 0) return 0;
        var last = parts.Last();
        return int.TryParse(last, out var seq) ? seq : 0;
    }
}
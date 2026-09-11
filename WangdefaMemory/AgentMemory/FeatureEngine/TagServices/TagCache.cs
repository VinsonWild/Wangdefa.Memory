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

    public void Add(TagEntry entry)
    {
        _tagCache[entry.Tag] = entry;
        _codeCache[entry.Code] = entry;
        var seq = ExtractSeq(entry.Code);
        if (seq > _nextSeq) _nextSeq = seq;
    }

    public void Update(TagEntry entry)
    {
        _tagCache[entry.Tag] = entry;
        _codeCache[entry.Code] = entry;
    }

    public void Remove(string tag, string code)
    {
        _tagCache.Remove(tag);
        _codeCache.Remove(code);
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
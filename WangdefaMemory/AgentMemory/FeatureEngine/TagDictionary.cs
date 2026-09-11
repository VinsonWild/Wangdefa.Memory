// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Wangdefa.AgentMemory.FeatureEngine.Models;
using Wangdefa.AgentMemory.FeatureEngine.TagServices;

namespace Wangdefa.AgentMemory.FeatureEngine;

/// <summary>
/// 标签池管理 - 词 ↔ code 映射（门面）
/// </summary>
public class TagDictionary
{
    private readonly TagCache _cache;
    private readonly TagStore _store;
    private readonly TagEvolutionService _evolution;
    private readonly TagSimilarityService _similarity;

    public const string StatusActive = "active";
    public const string StatusUnexamined = "unexamined";
    public const string StatusMerged = "merged";
    public const string StatusDeprecated = "deprecated";

    private PasswordBook? _passwordBook;

    public TagDictionary(FeatureEngineDb db)
    {
        _cache = new TagCache();
        _store = new TagStore(db);
        _evolution = new TagEvolutionService(_store, _cache);
        _similarity = new TagSimilarityService();
    }

    public void SetPasswordBook(PasswordBook passwordBook)
    {
        _passwordBook = passwordBook;
        _evolution.SetPasswordBook(passwordBook);
    }

    public void LoadAll()
    {
        if (_cache.IsFullyLoaded) return;

        var entries = _store.LoadAll();
        foreach (var entry in entries)
        {
            _cache.Add(entry);
        }
        _cache.SetFullyLoaded();
    }

    public string? GetCode(string tag)
    {
        var entry = _cache.GetByTag(tag) ?? _store.LoadFromDb(tag);
        return entry?.Code;
    }

    public string? GetCode(string tag, string dimension)
    {
        var cached = _cache.GetByTag(tag);
        if (cached != null && cached.Dimensions.Contains(dimension))
            return cached.Code;

        var entry = _store.LoadByTagAndDimension(tag, dimension);
        if (entry != null)
        {
            _cache.Add(entry);
            return entry.Code;
        }

        var fallback = _cache.GetByTag(tag) ?? _store.LoadFromDb(tag);
        return fallback?.Code;
    }

    public string? GetCodeByTagAndDefinitions(string tag, string[] definitions, string dimension)
    {
        var code = GetCode(tag, dimension);
        if (code != null) return code;

        var entries = GetAllTags().Where(e => e.Tag == tag && e.Status == "active");
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Definition)) continue;

            foreach (var def in definitions)
            {
                if (string.IsNullOrEmpty(def)) continue;

                if (entry.Definition.Contains(def, StringComparison.OrdinalIgnoreCase) ||
                    def.Contains(entry.Definition, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"✅ 子串匹配: {tag} → definition '{def}' 匹配到 '{entry.Definition}'");
                    return entry.Code;
                }
            }
        }

        return null;
    }

    public TagEntry? GetEntry(string tag)
    {
        return _cache.GetByTag(tag) ?? _store.LoadFromDb(tag);
    }

    public TagEntry? GetEntryByCode(string code)
    {
        return _cache.GetByCode(code) ?? _store.LoadFromDbByCode(code);
    }

    public List<string> GetRelatedCodes(string code)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return new List<string>();
        return JsonSerializer.Deserialize<List<string>>(entry.RelatedCodes) ?? new List<string>();
    }

    public TagEntry Add(string tag, string tagType = "content", string definition = "", string source = "auto")
    {
        var existing = _cache.GetByTag(tag) ?? _store.LoadFromDb(tag);
        if (existing != null)
            return existing;

        var code = $"TAG_{tagType.ToUpper()}_{NormalizeTag(tag)}_{_cache.NextSeq:D3}";

        var entry = new TagEntry
        {
            Tag = tag,
            Code = code,
            TagType = tagType,
            Definition = definition,
            Dimensions = "[]",
            RelatedCodes = "[]",
            Synonyms = "[]",
            Source = source,
            Status = "active",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _store.Insert(entry);
        _cache.Add(entry);

        return entry;
    }

    public TagEntry Add(string tag, string tagType, string definition, string dimension, string source)
    {
        var existing = _cache.GetByTag(tag) ?? _store.LoadFromDb(tag);
        if (existing != null)
        {
            // 已存在，检查并更新维度
            var dims = JsonSerializer.Deserialize<List<string>>(existing.Dimensions) ?? new List<string>();
            if (!string.IsNullOrEmpty(dimension) && !dims.Contains(dimension))
            {
                dims.Add(dimension);
                existing.Dimensions = JsonSerializer.Serialize(dims);
                _store.UpdateDimensions(existing.Code, existing.Dimensions);
                _cache.Update(existing);
            }
            return existing;
        }

        var code = $"TAG_{tagType.ToUpper()}_{NormalizeTag(tag)}_{_cache.NextSeq:D3}";
        var dimsJson = JsonSerializer.Serialize(new List<string> { dimension });

        var entry = new TagEntry
        {
            Tag = tag,
            Code = code,
            TagType = tagType,
            Definition = definition,
            Dimensions = dimsJson,
            RelatedCodes = "[]",
            Synonyms = "[]",
            Source = source,
            Status = "active",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _store.Insert(entry);
        _cache.Add(entry);

        return entry;
    }

    public TagEntry AddWithSynonyms(string tag, string tagType, string definition, string dimension, string source, string[]? synonyms = null, string status = "unexamined")
    {
        var existing = _cache.GetByTag(tag) ?? _store.LoadFromDb(tag);
        if (existing != null)
        {
            if (synonyms != null && synonyms.Length > 0)
            {
                MergeSynonyms(existing.Code, synonyms);
            }
            return existing;
        }

        var code = $"TAG_{tagType.ToUpper()}_{NormalizeTag(tag)}_{_cache.NextSeq:D3}";
        var dimsJson = JsonSerializer.Serialize(new List<string> { dimension });
        var synonymsJson = JsonSerializer.Serialize(synonyms ?? Array.Empty<string>());

        var entry = new TagEntry
        {
            Tag = tag,
            Code = code,
            TagType = tagType,
            Definition = definition,
            Dimensions = dimsJson,
            RelatedCodes = "[]",
            Synonyms = synonymsJson,
            Source = source,
            Status = status,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _store.Insert(entry);
        _cache.Add(entry);

        Console.WriteLine($"[TagDictionary] 新增标签: {tag} → {code}, 近义词: {synonymsJson}");
        return entry;
    }

    public void MergeSynonyms(string code, string[] newSynonyms)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return;

        var existingSynonyms = JsonSerializer.Deserialize<List<string>>(entry.Synonyms) ?? new List<string>();
        var added = false;

        foreach (var syn in newSynonyms)
        {
            if (string.IsNullOrEmpty(syn)) continue;
            if (!existingSynonyms.Contains(syn))
            {
                existingSynonyms.Add(syn);
                added = true;
            }
        }

        if (!added) return;

        var newJson = JsonSerializer.Serialize(existingSynonyms);
        entry.Synonyms = newJson;
        _store.UpdateSynonyms(code, newJson);
        _cache.Update(entry);

        Console.WriteLine($"[TagDictionary] 已合并近义词: {code} → {string.Join(", ", existingSynonyms)}");
    }

    public string[] GetSynonyms(string code)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return Array.Empty<string>();
        return JsonSerializer.Deserialize<string[]>(entry.Synonyms) ?? Array.Empty<string>();
    }

    public void UpdateDefinition(string code, string definition)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return;

        entry.Definition = definition;
        _store.UpdateDefinition(code, definition);
        _cache.Update(entry);
    }

    public void UpdateRelatedCodes(string code, List<string> relatedCodes)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return;

        var json = JsonSerializer.Serialize(relatedCodes);
        entry.RelatedCodes = json;
        _store.UpdateRelatedCodes(code, json);
        _cache.Update(entry);
    }

    public List<string> GetAllCodes()
    {
        LoadAll();
        return _cache.GetAllCodes();
    }

    public List<TagEntry> GetAllTags()
    {
        LoadAll();
        return _cache.GetAllTags();
    }

    public string GetTagsForPrompt()
    {
        LoadAll();
        var entries = _cache.GetAllTags()
            .Where(e => e.Status == "active")
            .Select(e =>
            {
                var line = $"- 标签：{e.Tag}，维度：{e.Dimensions}，编码：{e.Code}";
                if (!string.IsNullOrEmpty(e.Definition))
                    line += $"，语义：{e.Definition}";
                return line;
            })
            .ToList();
        return entries.Count > 0 ? string.Join("\n", entries) : "（标签池为空）";
    }

    public void Deprecate(string code, string? reason = null)
        => _evolution.Deprecate(code, reason);

    public void MergeTags(string sourceCode, string targetCode)
        => _evolution.MergeTags(sourceCode, targetCode);

    public void SplitTag(string sourceCode, string targetCode, string? reason = null)
        => _evolution.SplitTag(sourceCode, targetCode, reason);

    public void Activate(string code)
        => _evolution.Activate(code);

    public List<TagEntry> Search(string query)
    {
        LoadAll();
        return _cache.Search(query);
    }

    public List<TagEntry> GetUnexaminedTagsByNames(List<string> tagNames)
    {
        if (tagNames == null || tagNames.Count == 0) return new List<TagEntry>();
        LoadAll();
        return _cache.GetAllTags()
            .Where(e => tagNames.Contains(e.Tag) && e.Status == StatusUnexamined)
            .ToList();
    }

    public List<TagEntry> FindActiveByName(string tag)
    {
        LoadAll();
        return _cache.GetAllTags()
            .Where(e => e.Tag == tag && e.Status == StatusActive)
            .ToList();
    }

    public double CalculateSimilarity(TagEntry a, TagEntry b)
        => _similarity.CalculateSimilarity(a, b);

    private string NormalizeTag(string tag)
    {
        var normalized = new string(tag.Where(c => char.IsLetterOrDigit(c)).ToArray());
        return string.IsNullOrEmpty(normalized) ? "UNKNOWN" : normalized.ToUpper();
    }
}
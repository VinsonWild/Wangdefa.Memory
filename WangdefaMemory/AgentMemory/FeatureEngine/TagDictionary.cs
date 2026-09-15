// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0

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
        return ResolveCode(entry);
    }

    public string? GetCode(string tag, string dimension)
    {
        var cached = _cache.GetByTag(tag);
        if (cached != null && cached.Dimensions.Contains(dimension))
            return ResolveCode(cached);

        var entry = _store.LoadByTagAndDimension(tag, dimension);
        if (entry != null)
        {
            _cache.Add(entry);
            return ResolveCode(entry);
        }

        // fallback 有意不校验维度：保证老标签（dimension 为空）仍能被召回。
        // 维度对齐交给批次 E（版本对齐层）被动自愈处理。
        var fallback = _cache.GetByTag(tag) ?? _store.LoadFromDb(tag);
        return ResolveCode(fallback);
    }

    /// <summary>
    /// 解析 code：若命中的标签是 merged 状态，顺链重定向到目标 code
    /// 防环 + 跳数上限 3（最多经历 3 个 merged 标签，即 A→B→C→D，D 为终点）
    /// 异常时报警并返回 null
    /// </summary>
    private string? ResolveCode(TagEntry? entry)
    {
        if (entry == null) return null;
        if (entry.Status != StatusMerged) return entry.Code;

        var visited = new List<string>();
        var current = entry;
        string? lastMergedTo = null;

        while (current != null && current.Status == StatusMerged)
        {
            // 成环检测
            if (visited.Contains(current.Code))
            {
                Console.WriteLine($"[TagDictionary] ⚠️ 合并链成环: {string.Join("→", visited)}→{current.Code}");
                return null;
            }

            // 跳数上限：最多经历 3 个 merged 标签（即 A→B→C→D，D 为终点）
            // 检查在加入 visited 之前，所以 visited.Count >= 3 时说明已走过 3 个 merged
            if (visited.Count >= 3)
            {
                Console.WriteLine($"[TagDictionary] ⚠️ 合并链超过上限: {string.Join("→", visited)}→{current.Code}（已 {visited.Count} 个 merged 节点）");
                return null;
            }

            // MergedTo 为空
            if (string.IsNullOrEmpty(current.MergedTo))
            {
                Console.WriteLine($"[TagDictionary] ⚠️ merged 标签 {current.Code} 的 MergedTo 为空");
                return null;
            }

            visited.Add(current.Code);
            lastMergedTo = current.MergedTo;
            current = _cache.GetByCode(current.MergedTo)
                   ?? _store.LoadFromDbByCode(current.MergedTo);
        }

        if (current == null)
        {
            Console.WriteLine($"[TagDictionary] ⚠️ 合并链断链: {string.Join("→", visited)}→{lastMergedTo}（目标 code 不存在）");
            return null;
        }

        return current.Code;
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

    // ============================================================
    // 批次 E：版本对齐层
    // ============================================================

    /// <summary>
    /// 用当前模型比对：返回该数据相对当前版本的差距
    /// 空 gap = 当前版本；非空 = 需对齐
    /// 判据：
    ///   ① Definition 仅判空（长度判据实测无效）
    ///   ② Dimensions 空 / [] / 无有效元素
    ///   ③ RelatedCodes 非空且不是新格式，或含无效元素
    /// 明确不检查 Synonyms（为空是正常状态）
    /// </summary>
    public TagGap CompareWithCurrent(TagEntry entry)
    {
        var gap = new TagGap();
        if (entry == null) return gap;

        // ① Definition 仅判空
        if (string.IsNullOrWhiteSpace(entry.Definition))
            gap.Missing.Add("definition");

        // ② Dimensions：空 / [] / 无有效元素
        if (!HasValidElements(entry.Dimensions))
            gap.Missing.Add("dimensions");

        // ③ RelatedCodes：非空且不是新格式，或含无效元素
        if (IsLegacyRelated(entry.RelatedCodes) || (!IsEmptyJsonArray(entry.RelatedCodes) && !HasValidElements(entry.RelatedCodes)))
            gap.Legacy.Add("related_codes");

        return gap;
    }

    /// <summary>
    /// 格式残缺判断（等价于 CompareWithCurrent 非空）
    /// </summary>
    public bool IsMalformed(TagEntry entry)
    {
        if (entry == null) return false;
        return !CompareWithCurrent(entry).IsEmpty;
    }

    /// <summary>
    /// 批量检查：按标签名列表筛出残缺标签
    /// </summary>
    public List<TagEntry> GetMalformedTags(List<string> tagNames)
    {
        var result = new List<TagEntry>();
        if (tagNames == null || tagNames.Count == 0) return result;

        foreach (var name in tagNames.Distinct())
        {
            var entry = GetEntry(name);
            if (entry != null && IsMalformed(entry))
                result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// 更新维度（批次 E 对齐用）
    /// </summary>
    public void UpdateDimensions(string code, List<string> dimensions)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return;

        var json = JsonSerializer.Serialize(dimensions);
        entry.Dimensions = json;
        _store.UpdateDimensions(code, json);
        _cache.Update(entry);

        Console.WriteLine($"[TagDictionary] 维度已更新: {code} → {json}");
    }

    /// <summary>
    /// JSON 数组是否有有效元素（非空、非全空白）
    /// </summary>
    private bool HasValidElements(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return false;
        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(json);
            if (arr == null) return false;
            return arr.Any(s => !string.IsNullOrWhiteSpace(s));
        }
        catch
        {
            return false;
        }
    }

    private bool IsEmptyJsonArray(string json)
    {
        return string.IsNullOrWhiteSpace(json) || json == "[]";
    }

    /// <summary>
    /// 判断 RelatedCodes 是否为旧格式（字符串数组，而非对象数组）
    /// </summary>
    private bool IsLegacyRelated(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return false;
        // 新格式以 [{" 开头；旧格式以 [" 开头
        var trimmed = json.TrimStart();
        return !trimmed.StartsWith("[{");
    }

    // ============================================================
    // 关联读写
    // ============================================================

    /// <summary>
    /// 读取标签关联（转发到 TagEvolutionService）
    /// </summary>
    public List<TagRelation> GetRelations(string code)
        => _evolution.GetRelations(code);

    /// <summary>
    /// 建立关联（转发到 TagEvolutionService）
    /// </summary>
    public void AddRelation(string code, string targetCode, string level)
        => _evolution.AddRelation(code, targetCode, level);

    /// <summary>
    /// 【兼容】读取关联 code 列表（已改用 GetRelations，此方法仅保留向后兼容）
    /// </summary>
    [Obsolete("批次 A 已替换为 GetRelations，此方法仅保留向后兼容")]
    public List<string> GetRelatedCodes(string code)
        => GetRelations(code).Select(r => r.Code).ToList();

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

            // ★ 批次 C：同名标签的释义累积（不覆盖，追加去重）
            if (!string.IsNullOrEmpty(definition))
            {
                _evolution.MergeDefinitions(existing.Code, definition);
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
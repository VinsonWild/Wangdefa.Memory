using System.Text.Json;
using Microsoft.Data.Sqlite;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine;

/// <summary>
/// 标签池管理 - 词 ↔ code 映射
/// 支持懒加载，避免全量加载到内存
/// </summary>
public class TagDictionary
{
    private readonly FeatureEngineDb _db;
    private readonly Dictionary<string, TagEntry> _tagCache;   // tag → TagEntry
    private readonly Dictionary<string, TagEntry> _codeCache;  // code → TagEntry
    private int _nextSeq;
    private PasswordBook? _passwordBook;
    private bool _isFullyLoaded;

    public TagDictionary(FeatureEngineDb db)
    {
        _db = db;
        _tagCache = new Dictionary<string, TagEntry>();
        _codeCache = new Dictionary<string, TagEntry>();
        _isFullyLoaded = false;
    }

    public void SetPasswordBook(PasswordBook passwordBook)
    {
        _passwordBook = passwordBook;
    }

    private TagEntry? LoadFromDb(string tag)
    {
        if (_tagCache.TryGetValue(tag, out var cached))
            return cached;

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tag_dictionary WHERE tag = @tag AND status != 'deprecated'";
        cmd.Parameters.AddWithValue("@tag", tag);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var entry = BuildEntry(reader);
            _tagCache[entry.Tag] = entry;
            _codeCache[entry.Code] = entry;
            return entry;
        }

        return null;
    }

    private TagEntry? LoadFromDbByCode(string code)
    {
        if (_codeCache.TryGetValue(code, out var cached))
            return cached;

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tag_dictionary WHERE code = @code AND status != 'deprecated'";
        cmd.Parameters.AddWithValue("@code", code);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var entry = BuildEntry(reader);
            _tagCache[entry.Tag] = entry;
            _codeCache[entry.Code] = entry;
            return entry;
        }

        return null;
    }

    private TagEntry BuildEntry(SqliteDataReader reader)
    {
        return new TagEntry
        {
            TagId = reader.GetInt32(0),
            Tag = reader.GetString(1),
            Code = reader.GetString(2),
            TagType = reader.GetString(3),
            Definition = reader.GetString(4),
            Dimensions = reader.GetString(5),
            RelatedCodes = reader.GetString(6),
            Synonyms = reader.GetString(7),
            Source = reader.GetString(8),
            Status = reader.GetString(9),
            MergedTo = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt = reader.GetDateTime(11),
            UpdatedAt = reader.GetDateTime(12)
        };
    }

    public void LoadAll()
    {
        if (_isFullyLoaded) return;

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tag_dictionary WHERE status != 'deprecated'";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var entry = BuildEntry(reader);
            _tagCache[entry.Tag] = entry;
            _codeCache[entry.Code] = entry;

            var seq = ExtractSeq(entry.Code);
            if (seq > _nextSeq) _nextSeq = seq;
        }

        _isFullyLoaded = true;
    }

    private int ExtractSeq(string code)
    {
        var parts = code.Split('_');
        if (parts.Length == 0) return 0;
        var last = parts.Last();
        return int.TryParse(last, out var seq) ? seq : 0;
    }

    public string? GetCode(string tag)
    {
        var entry = LoadFromDb(tag);
        return entry?.Code;
    }

    public string? GetCode(string tag, string dimension)
    {
        if (_tagCache.TryGetValue(tag, out var cached))
        {
            if (cached.Dimensions.Contains(dimension))
                return cached.Code;
        }

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tag_dictionary WHERE tag = @tag AND dimensions LIKE @dim AND status != 'deprecated'";
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.Parameters.AddWithValue("@dim", $"%{dimension}%");

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var entry = BuildEntry(reader);
            _tagCache[entry.Tag] = entry;
            _codeCache[entry.Code] = entry;
            return entry.Code;
        }

        var fallback = LoadFromDb(tag);
        if (fallback != null) return fallback.Code;

        return Add(tag, "content", definition: "", dimension: dimension, source: "auto").Code;
    }

    public TagEntry? GetEntry(string tag)
    {
        return LoadFromDb(tag);
    }

    public TagEntry? GetEntryByCode(string code)
    {
        return LoadFromDbByCode(code);
    }

    public List<string> GetRelatedCodes(string code)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return new List<string>();
        return JsonSerializer.Deserialize<List<string>>(entry.RelatedCodes) ?? new List<string>();
    }

    public TagEntry Add(string tag, string tagType = "content", string definition = "", string source = "auto")
    {
        var existing = LoadFromDb(tag);
        if (existing != null)
            return existing;

        using var conn = _db.GetConnection();
        conn.Open();

        var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT MAX(CAST(SUBSTR(code, LENGTH(code) - 2) AS INTEGER)) FROM tag_dictionary WHERE code LIKE 'TAG_%'";
        var maxSeq = countCmd.ExecuteScalar() as long? ?? 0;
        _nextSeq = (int)maxSeq + 1;

        var code = $"TAG_{tagType.ToUpper()}_{NormalizeTag(tag)}_{_nextSeq:D3}";

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

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO tag_dictionary (tag, code, tag_type, definition, dimensions, related_codes, synonyms, source, status)
            VALUES (@tag, @code, @type, @def, '[]', '[]', '[]', @source, 'active')
        ";
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@type", tagType);
        cmd.Parameters.AddWithValue("@def", definition);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        entry.TagId = Convert.ToInt32(cmd.ExecuteScalar());

        _tagCache[tag] = entry;
        _codeCache[code] = entry;

        return entry;
    }

    public TagEntry Add(string tag, string tagType, string definition, string dimension, string source)
    {
        var existing = LoadFromDb(tag);
        if (existing != null)
        {
            var dims = JsonSerializer.Deserialize<List<string>>(existing.Dimensions) ?? new List<string>();
            if (!dims.Contains(dimension))
            {
                dims.Add(dimension);
                existing.Dimensions = JsonSerializer.Serialize(dims);
                UpdateDimensions(existing.Code, existing.Dimensions);
            }
            return existing;
        }

        using var conn = _db.GetConnection();
        conn.Open();

        var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT MAX(CAST(SUBSTR(code, LENGTH(code) - 2) AS INTEGER)) FROM tag_dictionary WHERE code LIKE 'TAG_%'";
        var maxSeq = countCmd.ExecuteScalar() as long? ?? 0;
        _nextSeq = (int)maxSeq + 1;

        var code = $"TAG_{tagType.ToUpper()}_{NormalizeTag(tag)}_{_nextSeq:D3}";
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

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO tag_dictionary (tag, code, tag_type, definition, dimensions, related_codes, synonyms, source, status)
            VALUES (@tag, @code, @type, @def, @dims, '[]', '[]', @source, 'active')
        ";
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@type", tagType);
        cmd.Parameters.AddWithValue("@def", definition);
        cmd.Parameters.AddWithValue("@dims", dimsJson);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        entry.TagId = Convert.ToInt32(cmd.ExecuteScalar());

        _tagCache[tag] = entry;
        _codeCache[code] = entry;

        return entry;
    }

    public TagEntry AddWithSynonyms(string tag, string tagType, string definition, string dimension, string source, string[]? synonyms = null)
    {
        var existing = LoadFromDb(tag);
        if (existing != null)
        {
            if (synonyms != null && synonyms.Length > 0)
            {
                MergeSynonyms(existing.Code, synonyms);
            }
            return existing;
        }

        using var conn = _db.GetConnection();
        conn.Open();

        var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT MAX(CAST(SUBSTR(code, LENGTH(code) - 2) AS INTEGER)) FROM tag_dictionary WHERE code LIKE 'TAG_%'";
        var maxSeq = countCmd.ExecuteScalar() as long? ?? 0;
        _nextSeq = (int)maxSeq + 1;

        var code = $"TAG_{tagType.ToUpper()}_{NormalizeTag(tag)}_{_nextSeq:D3}";
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
            Status = "active",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO tag_dictionary (tag, code, tag_type, definition, dimensions, related_codes, synonyms, source, status)
            VALUES (@tag, @code, @type, @def, @dims, '[]', @synonyms, @source, 'active')
        ";
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@type", tagType);
        cmd.Parameters.AddWithValue("@def", definition);
        cmd.Parameters.AddWithValue("@dims", dimsJson);
        cmd.Parameters.AddWithValue("@synonyms", synonymsJson);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        entry.TagId = Convert.ToInt32(cmd.ExecuteScalar());

        _tagCache[tag] = entry;
        _codeCache[code] = entry;

        if (synonyms != null && synonyms.Length > 0)
        {
            foreach (var syn in synonyms)
            {
                if (string.IsNullOrEmpty(syn)) continue;
                var synEntry = LoadFromDb(syn);
                if (synEntry != null)
                {
                    MergeSynonyms(synEntry.Code, new[] { tag });
                }
                else
                {
                    AddWithSynonyms(syn, tagType, "", dimension, "auto", new[] { tag });
                }
            }
        }

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

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET synonyms = @synonyms, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@synonyms", newJson);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();

        Console.WriteLine($"[TagDictionary] 已合并近义词: {code} → {string.Join(", ", existingSynonyms)}");
    }

    public string[] GetSynonyms(string code)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return Array.Empty<string>();
        return JsonSerializer.Deserialize<string[]>(entry.Synonyms) ?? Array.Empty<string>();
    }

    private void UpdateDimensions(string code, string dimensionsJson)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET dimensions = @dims, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@dims", dimensionsJson);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    public void UpdateRelatedCodes(string code, List<string> relatedCodes)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return;

        var json = JsonSerializer.Serialize(relatedCodes);
        entry.RelatedCodes = json;

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET related_codes = @related, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@related", json);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    public void UpdateDefinition(string code, string definition)
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return;

        entry.Definition = definition;

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET definition = @def, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@def", definition);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    public List<string> GetAllCodes()
    {
        LoadAll();
        return _codeCache.Keys.ToList();
    }

    public List<TagEntry> GetAllTags()
    {
        LoadAll();
        return _tagCache.Values.ToList();
    }

    public string GetTagsForPrompt()
    {
        LoadAll();
        var entries = _tagCache.Values
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
    {
        var entry = GetEntryByCode(code);
        if (entry == null) return;

        entry.Status = "deprecated";
        if (!string.IsNullOrEmpty(reason))
            entry.Definition = reason;

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET status = 'deprecated', definition = @def, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@def", entry.Definition);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();

        Console.WriteLine($"[TagDictionary] 已弃用: {code}");
    }

    public void MergeTags(string sourceCode, string targetCode)
    {
        var sourceEntry = GetEntryByCode(sourceCode);
        var targetEntry = GetEntryByCode(targetCode);
        if (sourceEntry == null || targetEntry == null) return;
        if (_passwordBook == null)
        {
            Console.WriteLine("[TagDictionary] PasswordBook 未设置，无法执行合并");
            return;
        }

        var cards = _passwordBook.GetCards(sourceCode);
        if (cards.Count == 0)
        {
            sourceEntry.Status = "merged";
            sourceEntry.MergedTo = targetCode;

            using var conn = _db.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE tag_dictionary SET status = 'merged', merged_to = @target, updated_at = CURRENT_TIMESTAMP WHERE code = @source";
            cmd.Parameters.AddWithValue("@target", targetCode);
            cmd.Parameters.AddWithValue("@source", sourceCode);
            cmd.ExecuteNonQuery();
            Console.WriteLine($"[TagDictionary] 已合并（无卡片迁移）: {sourceCode} → {targetCode}");
            return;
        }

        _passwordBook.MoveCards(sourceCode, targetCode);

        sourceEntry.Status = "merged";
        sourceEntry.MergedTo = targetCode;

        using var conn2 = _db.GetConnection();
        conn2.Open();

        var cmd2 = conn2.CreateCommand();
        cmd2.CommandText = "UPDATE tag_dictionary SET status = 'merged', merged_to = @target, updated_at = CURRENT_TIMESTAMP WHERE code = @source";
        cmd2.Parameters.AddWithValue("@target", targetCode);
        cmd2.Parameters.AddWithValue("@source", sourceCode);
        cmd2.ExecuteNonQuery();

        Console.WriteLine($"[TagDictionary] 已合并: {sourceCode} → {targetCode}, {cards.Count} 张卡片");
    }

    public void SplitTag(string sourceCode, string targetCode, string? reason = null)
    {
        var sourceEntry = GetEntryByCode(sourceCode);
        if (sourceEntry == null) return;
        if (_passwordBook == null)
        {
            Console.WriteLine("[TagDictionary] PasswordBook 未设置，无法执行分裂");
            return;
        }

        var targetEntry = GetEntryByCode(targetCode);
        if (targetEntry == null)
        {
            Add(targetCode, "content", reason ?? "", "auto");
        }

        var cards = _passwordBook.GetCards(sourceCode);
        if (cards.Count == 0)
        {
            sourceEntry.Status = "deprecated";
            if (!string.IsNullOrEmpty(reason))
                sourceEntry.Definition = reason;

            using var conn = _db.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE tag_dictionary SET status = 'deprecated', definition = @def, updated_at = CURRENT_TIMESTAMP WHERE code = @source";
            cmd.Parameters.AddWithValue("@def", sourceEntry.Definition);
            cmd.Parameters.AddWithValue("@source", sourceCode);
            cmd.ExecuteNonQuery();
            Console.WriteLine($"[TagDictionary] 已分裂（无卡片迁移）: {sourceCode} → {targetCode}");
            return;
        }

        _passwordBook.MoveCards(sourceCode, targetCode);

        sourceEntry.Status = "deprecated";
        if (!string.IsNullOrEmpty(reason))
            sourceEntry.Definition = reason;

        using var conn2 = _db.GetConnection();
        conn2.Open();

        var cmd2 = conn2.CreateCommand();
        cmd2.CommandText = "UPDATE tag_dictionary SET status = 'deprecated', definition = @def, updated_at = CURRENT_TIMESTAMP WHERE code = @source";
        cmd2.Parameters.AddWithValue("@def", sourceEntry.Definition);
        cmd2.Parameters.AddWithValue("@source", sourceCode);
        cmd2.ExecuteNonQuery();

        Console.WriteLine($"[TagDictionary] 已分裂: {sourceCode} → {targetCode}, {cards.Count} 张卡片");
    }

    private string NormalizeTag(string tag)
    {
        var normalized = new string(tag.Where(c => char.IsLetterOrDigit(c)).ToArray());
        return string.IsNullOrEmpty(normalized) ? "UNKNOWN" : normalized.ToUpper();
    }

    public List<TagEntry> Search(string query)
    {
        LoadAll();
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
}
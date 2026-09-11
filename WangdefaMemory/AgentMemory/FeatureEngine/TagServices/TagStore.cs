// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine.TagServices;

/// <summary>
/// 标签数据库存储 - CRUD 操作
/// </summary>
public class TagStore
{
    private readonly FeatureEngineDb _db;

    public TagStore(FeatureEngineDb db)
    {
        _db = db;
    }

    public TagEntry? LoadFromDb(string tag)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tag_dictionary WHERE tag = @tag AND status != 'deprecated'";
        cmd.Parameters.AddWithValue("@tag", tag);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return BuildEntry(reader);
        }

        return null;
    }

    public TagEntry? LoadFromDbByCode(string code)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tag_dictionary WHERE code = @code AND status != 'deprecated'";
        cmd.Parameters.AddWithValue("@code", code);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return BuildEntry(reader);
        }

        return null;
    }

    /// <summary>
    /// 按 tag + dimension 查询标签
    /// </summary>
    public TagEntry? LoadByTagAndDimension(string tag, string dimension)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tag_dictionary WHERE tag = @tag AND dimensions LIKE @dim AND status != 'deprecated'";
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.Parameters.AddWithValue("@dim", $"%{dimension}%");

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return BuildEntry(reader);
        }
        return null;
    }

    public List<TagEntry> LoadAll()
    {
        var entries = new List<TagEntry>();

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tag_dictionary WHERE status != 'deprecated'";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            entries.Add(BuildEntry(reader));
        }

        return entries;
    }

    public TagEntry Insert(TagEntry entry)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO tag_dictionary (tag, code, tag_type, definition, dimensions, related_codes, synonyms, source, status)
            VALUES (@tag, @code, @type, @def, @dims, @related, @synonyms, @source, @status)
        ";
        cmd.Parameters.AddWithValue("@tag", entry.Tag);
        cmd.Parameters.AddWithValue("@code", entry.Code);
        cmd.Parameters.AddWithValue("@type", entry.TagType);
        cmd.Parameters.AddWithValue("@def", entry.Definition);
        cmd.Parameters.AddWithValue("@dims", entry.Dimensions);
        cmd.Parameters.AddWithValue("@related", entry.RelatedCodes);
        cmd.Parameters.AddWithValue("@synonyms", entry.Synonyms);
        cmd.Parameters.AddWithValue("@source", entry.Source);
        cmd.Parameters.AddWithValue("@status", entry.Status);
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid()";
        entry.TagId = Convert.ToInt32(cmd.ExecuteScalar());

        return entry;
    }

    public void UpdateStatus(string code, string status, string? mergedTo = null)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        if (mergedTo != null)
        {
            cmd.CommandText = "UPDATE tag_dictionary SET status = @status, merged_to = @merged, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
            cmd.Parameters.AddWithValue("@merged", mergedTo);
        }
        else
        {
            cmd.CommandText = "UPDATE tag_dictionary SET status = @status, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        }
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    public void UpdateDefinition(string code, string definition)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET definition = @def, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@def", definition);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    public void UpdateDimensions(string code, string dimensionsJson)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET dimensions = @dims, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@dims", dimensionsJson);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    public void UpdateSynonyms(string code, string synonymsJson)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET synonyms = @synonyms, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@synonyms", synonymsJson);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    public void UpdateRelatedCodes(string code, string relatedCodesJson)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tag_dictionary SET related_codes = @related, updated_at = CURRENT_TIMESTAMP WHERE code = @code";
        cmd.Parameters.AddWithValue("@related", relatedCodesJson);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
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
}
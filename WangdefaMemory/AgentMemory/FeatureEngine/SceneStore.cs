// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Wangdefa.AgentMemory.FeatureEngine;

/// <summary>
/// 场景库 - 独立存储场景大类+细分
/// </summary>
public class SceneStore
{
    private readonly string _connectionString;

    public SceneStore(string basePath)
    {
        var dbPath = Path.Combine(basePath, "scene_store.db");
        _connectionString = $"Data Source={dbPath}";
        EnsureDatabase();
    }

    private void EnsureDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS scene_dictionary (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                code TEXT UNIQUE NOT NULL,
                category TEXT NOT NULL,
                sub TEXT NOT NULL,
                name TEXT NOT NULL,
                synonyms TEXT NOT NULL DEFAULT '[]',
                status TEXT NOT NULL DEFAULT 'active',
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_scene_category ON scene_dictionary(category);
            CREATE INDEX IF NOT EXISTS idx_scene_code ON scene_dictionary(code);
        ";
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection GetConnection() => new SqliteConnection(_connectionString);

    /// <summary>
    /// 添加场景（直接入库）
    /// </summary>
    public void Add(string category, string sub, string name, string[]? synonyms = null)
    {
        var code = $"SCENE_{category.ToUpper()}_{NormalizeName(name)}_{DateTime.Now.Ticks % 1000:D3}";
        var synonymsJson = JsonSerializer.Serialize(synonyms ?? Array.Empty<string>());

        using var conn = GetConnection();
        conn.Open();

        // 检查是否已存在
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT code FROM scene_dictionary WHERE category = @category AND sub = @sub AND name = @name AND status != 'deprecated'";
        checkCmd.Parameters.AddWithValue("@category", category);
        checkCmd.Parameters.AddWithValue("@sub", sub);
        checkCmd.Parameters.AddWithValue("@name", name);
        var existing = checkCmd.ExecuteScalar() as string;
        if (existing != null) return;

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO scene_dictionary (code, category, sub, name, synonyms, status)
            VALUES (@code, @category, @sub, @name, @synonyms, 'active')
        ";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@sub", sub);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@synonyms", synonymsJson);
        cmd.ExecuteNonQuery();

        Console.WriteLine($"[SceneStore] 新增场景: {category}/{sub} → {name}");
    }

    /// <summary>
    /// 查场景（按大类+细分）
    /// </summary>
    public SceneEntry? Get(string category, string sub, string name)
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM scene_dictionary WHERE category = @category AND sub = @sub AND name = @name AND status = 'active'";
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@sub", sub);
        cmd.Parameters.AddWithValue("@name", name);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new SceneEntry
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                Category = reader.GetString(2),
                Sub = reader.GetString(3),
                Name = reader.GetString(4),
                Synonyms = reader.GetString(5),
                Status = reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                UpdatedAt = reader.GetDateTime(8)
            };
        }
        return null;
    }

    /// <summary>
    /// 按大类查场景
    /// </summary>
    public List<SceneEntry> GetByCategory(string category)
    {
        var result = new List<SceneEntry>();
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM scene_dictionary WHERE category = @category AND status = 'active'";
        cmd.Parameters.AddWithValue("@category", category);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SceneEntry
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                Category = reader.GetString(2),
                Sub = reader.GetString(3),
                Name = reader.GetString(4),
                Synonyms = reader.GetString(5),
                Status = reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                UpdatedAt = reader.GetDateTime(8)
            });
        }
        return result;
    }

    /// <summary>
    /// 模糊搜索场景（按名称或近义词）
    /// </summary>
    public List<SceneEntry> Search(string query)
    {
        var result = new List<SceneEntry>();
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM scene_dictionary WHERE status = 'active' AND (name LIKE @query OR synonyms LIKE @query)";
        cmd.Parameters.AddWithValue("@query", $"%{query}%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SceneEntry
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                Category = reader.GetString(2),
                Sub = reader.GetString(3),
                Name = reader.GetString(4),
                Synonyms = reader.GetString(5),
                Status = reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                UpdatedAt = reader.GetDateTime(8)
            });
        }
        return result;
    }

    private string NormalizeName(string name)
    {
        var normalized = new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray());
        return string.IsNullOrEmpty(normalized) ? "UNKNOWN" : normalized.ToUpper();
    }
}

public class SceneEntry
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Category { get; set; } = "";
    public string Sub { get; set; } = "";
    public string Name { get; set; } = "";
    public string Synonyms { get; set; } = "[]";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
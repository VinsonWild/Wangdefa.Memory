using Microsoft.Data.Sqlite;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine;

public class FeatureEngineDb
{
    private readonly string _connectionString;

    public FeatureEngineDb(string basePath)
    {
        var dbPath = Path.Combine(basePath, "feature_pool.db");
        _connectionString = $"Data Source={dbPath}";
        EnsureDatabase();
    }

    private void EnsureDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            -- 标签池
            CREATE TABLE IF NOT EXISTS tag_dictionary (
                tag_id INTEGER PRIMARY KEY AUTOINCREMENT,
                tag TEXT UNIQUE NOT NULL,
                code TEXT UNIQUE NOT NULL,
                tag_type TEXT NOT NULL,
                definition TEXT NOT NULL DEFAULT '',
                dimensions TEXT NOT NULL DEFAULT '[]',
                related_codes TEXT NOT NULL DEFAULT '[]',
                synonyms TEXT NOT NULL DEFAULT '[]',
                source TEXT NOT NULL DEFAULT 'auto',
                status TEXT NOT NULL DEFAULT 'active',
                merged_to TEXT,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            -- 密码簿
            CREATE TABLE IF NOT EXISTS password_book (
                code TEXT NOT NULL,
                card_id TEXT NOT NULL,
                card_type TEXT NOT NULL DEFAULT 'cognitive',
                topic_id TEXT,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (code, card_id)
            );

            -- 特征统计
            CREATE TABLE IF NOT EXISTS feature_stats (
                code TEXT PRIMARY KEY,
                hit_count INTEGER DEFAULT 0,
                last_hit DATETIME,
                first_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
                association_count INTEGER DEFAULT 0,
                avg_weight REAL DEFAULT 0.5
            );

            -- 共现记录
            CREATE TABLE IF NOT EXISTS co_occurrence (
                code1 TEXT NOT NULL,
                code2 TEXT NOT NULL,
                count INTEGER DEFAULT 1,
                last_seen DATETIME,
                PRIMARY KEY (code1, code2)
            );

            -- 索引
            CREATE INDEX IF NOT EXISTS idx_password_book_code ON password_book(code);
            CREATE INDEX IF NOT EXISTS idx_password_book_card_id ON password_book(card_id);
            CREATE INDEX IF NOT EXISTS idx_tag_dictionary_tag ON tag_dictionary(tag);
            CREATE INDEX IF NOT EXISTS idx_tag_dimension ON tag_dictionary(tag, dimensions);
        ";
        cmd.ExecuteNonQuery();

        SeedSystemTags();
    }

    private void SeedSystemTags()
    {
        var tags = new[]
        {
            new { tag = "工作", code = "TAG_SCENE_WORK", type = "scene", def = "工作场景" },
            new { tag = "生活", code = "TAG_SCENE_LIFE", type = "scene", def = "生活场景" },
            new { tag = "学习", code = "TAG_SCENE_STUDY", type = "scene", def = "学习场景" },
            new { tag = "娱乐", code = "TAG_SCENE_ENTERTAINMENT", type = "scene", def = "娱乐场景" },
            new { tag = "查询", code = "TAG_TASK_QUERY", type = "task", def = "用户查询信息" },
            new { tag = "创作", code = "TAG_TASK_CREATION", type = "task", def = "用户创作内容" },
            new { tag = "规划", code = "TAG_TASK_PLANNING", type = "task", def = "用户制定计划" },
            new { tag = "执行", code = "TAG_TASK_EXECUTE", type = "task", def = "用户执行操作" },
            new { tag = "闲聊", code = "TAG_TASK_CHAT", type = "task", def = "用户闲聊" },
            new { tag = "需要参考", code = "TAG_CONSTRAINT_REFERENCE", type = "constraint", def = "用户需要参考已有资料" },
            new { tag = "要有新意", code = "TAG_CONSTRAINT_INNOVATION", type = "constraint", def = "用户需要创新、差异化" },
            new { tag = "要可执行", code = "TAG_CONSTRAINT_ACTIONABLE", type = "constraint", def = "用户需要具体可执行" },
        };

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        foreach (var t in tags)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO tag_dictionary (tag, code, tag_type, definition, source)
                VALUES (@tag, @code, @type, @def, 'system')
            ";
            cmd.Parameters.AddWithValue("@tag", t.tag);
            cmd.Parameters.AddWithValue("@code", t.code);
            cmd.Parameters.AddWithValue("@type", t.type);
            cmd.Parameters.AddWithValue("@def", t.def);
            cmd.ExecuteNonQuery();
        }
    }

    public SqliteConnection GetConnection() => new SqliteConnection(_connectionString);
}
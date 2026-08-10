using Microsoft.Data.Sqlite;
using System.ComponentModel;
using System.Text.Json;
using Wangdefa.AgentMemory.Interfaces;

namespace Wangdefa.Tools;

public class SQLiteTools : ISQLiteTools
{
    private static string _dbPath = null!;

    public static void SetBasePath(string basePath)
    {
        _dbPath = Path.Combine(basePath, "wangdefa_memory.db");
    }

    [Description("写入全量记录到 SQLite 思考层")]
    public async Task<string> WriteRecord(
        string userInput,
        string agentResponse,
        string topicId,
        string tags = "",
        string summary = "",
        double confidence = 0.0,
        string perception = "",
        string route = "shallow",
        string overview = "")
    {
        try
        {
            if (string.IsNullOrEmpty(_dbPath))
            {
                throw new InvalidOperationException("SQLiteTools 未初始化，请先调用 SetBasePath");
            }

            var connectionString = $"Data Source={_dbPath}";
            using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();

            var createCmd = conn.CreateCommand();
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS memory_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_input TEXT NOT NULL,
                    agent_response TEXT NOT NULL,
                    topic_id TEXT NOT NULL,
                    tags TEXT,
                    summary TEXT,
                    confidence REAL,
                    perception TEXT,
                    route TEXT,
                    overview TEXT,
                    created_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_topic_id ON memory_records(topic_id);
                CREATE INDEX IF NOT EXISTS idx_created_at ON memory_records(created_at);
                CREATE INDEX IF NOT EXISTS idx_route ON memory_records(route);
            ";
            await createCmd.ExecuteNonQueryAsync();

            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO memory_records (
                    user_input, agent_response, topic_id, tags, summary, confidence,
                    perception, route, overview, created_at
                ) VALUES (
                    @user_input, @agent_response, @topic_id, @tags, @summary, @confidence,
                    @perception, @route, @overview, @created_at
                );
            ";
            insertCmd.Parameters.AddWithValue("@user_input", userInput);
            insertCmd.Parameters.AddWithValue("@agent_response", agentResponse);
            insertCmd.Parameters.AddWithValue("@topic_id", topicId);
            insertCmd.Parameters.AddWithValue("@tags", tags);
            insertCmd.Parameters.AddWithValue("@summary", summary);
            insertCmd.Parameters.AddWithValue("@confidence", confidence);
            insertCmd.Parameters.AddWithValue("@perception", perception);
            insertCmd.Parameters.AddWithValue("@route", route);
            insertCmd.Parameters.AddWithValue("@overview", overview);
            insertCmd.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("o"));

            await insertCmd.ExecuteNonQueryAsync();
            return $"✅ 全量记录已写入 SQLite，topicId: {topicId}";
        }
        catch (Exception ex)
        {
            return $"⚠️ 写入 SQLite 失败: {ex.Message}";
        }
    }
}
using Microsoft.Data.Sqlite;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine;

/// <summary>
/// 特征统计管理
/// </summary>
public class FeatureStats
{
    private readonly FeatureEngineDb _db;
    private readonly Dictionary<string, FeatureStat> _stats;

    public FeatureStats(FeatureEngineDb db)
    {
        _db = db;
        _stats = new Dictionary<string, FeatureStat>();
        LoadAll();
    }

    private void LoadAll()
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM feature_stats";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var stat = new FeatureStat
            {
                Code = reader.GetString(0),
                HitCount = reader.GetInt32(1),
                LastHit = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                FirstSeen = reader.GetDateTime(3),
                AssociationCount = reader.GetInt32(4),
                AvgWeight = reader.GetDouble(5)
            };
            _stats[stat.Code] = stat;
        }
    }

    /// <summary>
    /// 记录特征命中
    /// </summary>
    public void RecordHit(List<string> codes)
    {
        foreach (var code in codes)
        {
            RecordHit(code);
        }
    }

    /// <summary>
    /// 记录单个特征命中
    /// </summary>
    public void RecordHit(string code)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO feature_stats (code, hit_count, last_hit, first_seen)
            VALUES (@code, 1, @now, @now)
            ON CONFLICT(code) DO UPDATE SET
                hit_count = hit_count + 1,
                last_hit = @now
        ";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@now", DateTime.Now);
        cmd.ExecuteNonQuery();

        // 更新缓存
        if (_stats.TryGetValue(code, out var stat))
        {
            stat.HitCount++;
            stat.LastHit = DateTime.Now;
        }
        else
        {
            _stats[code] = new FeatureStat
            {
                Code = code,
                HitCount = 1,
                LastHit = DateTime.Now,
                FirstSeen = DateTime.Now,
                AssociationCount = 0,
                AvgWeight = 0.5
            };
        }
    }

    /// <summary>
    /// 获取特征统计
    /// </summary>
    public FeatureStat? GetStat(string code)
    {
        return _stats.TryGetValue(code, out var stat) ? stat : null;
    }

    /// <summary>
    /// 获取所有统计
    /// </summary>
    public List<FeatureStat> GetAll()
    {
        return _stats.Values.ToList();
    }

    /// <summary>
    /// 按命中次数排序（高频优先）
    /// </summary>
    public List<string> GetTopCodes(int topN = 20)
    {
        return _stats.Values
            .OrderByDescending(s => s.HitCount)
            .Take(topN)
            .Select(s => s.Code)
            .ToList();
    }
}
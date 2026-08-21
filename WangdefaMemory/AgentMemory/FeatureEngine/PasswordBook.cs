using Microsoft.Data.Sqlite;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine;

/// <summary>
/// 密码簿管理 - code → 卡片ID列表
/// 带 LFU 缓存（最不常用淘汰）
/// </summary>
public class PasswordBook
{
    private readonly FeatureEngineDb _db;
    private readonly Dictionary<string, (HashSet<string> Cards, int HitCount)> _cache;
    private readonly int _maxCache;

    public PasswordBook(FeatureEngineDb db, int maxCache = 5000)
    {
        _db = db;
        _cache = new Dictionary<string, (HashSet<string>, int)>();
        _maxCache = maxCache;
    }

    public HashSet<string> GetCards(string code)
    {
        // 1. 查缓存
        if (_cache.TryGetValue(code, out var entry))
        {
            _cache[code] = (entry.Cards, entry.HitCount + 1);
            return new HashSet<string>(entry.Cards);
        }

        // 2. 查 SQLite
        var result = new HashSet<string>();
        using var conn = _db.GetConnection();
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT card_id FROM password_book WHERE code = @code";
        cmd.Parameters.AddWithValue("@code", code);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));

        // 3. 写入缓存（LFU 淘汰）
        if (_cache.Count >= _maxCache)
        {
            var minKey = _cache.OrderBy(x => x.Value.HitCount).First().Key;
            _cache.Remove(minKey);
        }
        _cache[code] = (result, 1);

        return new HashSet<string>(result);
    }

    public HashSet<string> GetCodesByCard(string cardId)
    {
        var result = new HashSet<string>();
        using var conn = _db.GetConnection();
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT code FROM password_book WHERE card_id = @card_id";
        cmd.Parameters.AddWithValue("@card_id", cardId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result;
    }

    public void Add(string code, string cardId)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(cardId)) return;

        using var conn = _db.GetConnection();
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR IGNORE INTO password_book (code, card_id, card_type)
                    VALUES (@code, @card_id, @card_type)";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@card_id", cardId);
        cmd.Parameters.AddWithValue("@card_type", "cognitive");
        cmd.ExecuteNonQuery();

        if (_cache.TryGetValue(code, out var entry))
        {
            entry.Cards.Add(cardId);
            _cache[code] = (entry.Cards, entry.HitCount);
        }
    }

    public void AddCodesToCard(string cardId, List<string> codes)
    {
        foreach (var code in codes)
            Add(code, cardId);
    }

    public void Remove(string code, string cardId)
    {
        using var conn = _db.GetConnection();
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM password_book WHERE code = @code AND card_id = @card_id";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@card_id", cardId);
        cmd.ExecuteNonQuery();

        if (_cache.TryGetValue(code, out var entry))
        {
            entry.Cards.Remove(cardId);
            if (entry.Cards.Count == 0)
                _cache.Remove(code);
            else
                _cache[code] = (entry.Cards, entry.HitCount);
        }
    }

    public HashSet<string> GetIntersection(List<string> codes)
    {
        if (codes == null || codes.Count == 0)
            return new HashSet<string>();

        var result = GetCards(codes[0]);
        for (int i = 1; i < codes.Count; i++)
        {
            var cards = GetCards(codes[i]);
            result.IntersectWith(cards);
            if (result.Count == 0) break;
        }
        return result;
    }

    public HashSet<string> GetUnion(List<string> codes)
    {
        if (codes == null || codes.Count == 0)
            return new HashSet<string>();

        var result = new HashSet<string>();
        foreach (var code in codes)
        {
            var cards = GetCards(code);
            result.UnionWith(cards);
        }
        return result;
    }

    public void MoveCards(string sourceCode, string targetCode)
    {
        var cards = GetCards(sourceCode);
        if (cards.Count == 0) return;

        foreach (var cardId in cards)
        {
            Add(targetCode, cardId);
            Remove(sourceCode, cardId);
        }

        Console.WriteLine($"[PasswordBook] 已迁移: {sourceCode} → {targetCode}, {cards.Count} 张");
    }

    public List<string> GetAllCodes()
    {
        var result = new List<string>();
        using var conn = _db.GetConnection();
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT code FROM password_book";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result;
    }

    public int GetCardCount(string code)
    {
        using var conn = _db.GetConnection();
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM password_book WHERE code = @code";
        cmd.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
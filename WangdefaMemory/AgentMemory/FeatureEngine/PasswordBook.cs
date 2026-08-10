using Microsoft.Data.Sqlite;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine;

/// <summary>
/// 密码簿管理 - code → 卡片ID列表
/// </summary>
public class PasswordBook
{
    private readonly FeatureEngineDb _db;
    private readonly Dictionary<string, HashSet<string>> _book;   // code → 卡片ID集合
    private readonly Dictionary<string, HashSet<string>> _reverseBook; // 卡片ID → code集合

    public PasswordBook(FeatureEngineDb db)
    {
        _db = db;
        _book = new Dictionary<string, HashSet<string>>();
        _reverseBook = new Dictionary<string, HashSet<string>>();
        LoadAll();
    }

    /// <summary>
    /// 加载所有数据到内存
    /// </summary>
    private void LoadAll()
    {
        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT code, card_id FROM password_book";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var code = reader.GetString(0);
            var cardId = reader.GetString(1);
            AddToMemory(code, cardId);
        }
    }

    private void AddToMemory(string code, string cardId)
    {
        if (!_book.ContainsKey(code))
            _book[code] = new HashSet<string>();
        _book[code].Add(cardId);

        if (!_reverseBook.ContainsKey(cardId))
            _reverseBook[cardId] = new HashSet<string>();
        _reverseBook[cardId].Add(code);
    }

    /// <summary>
    /// 获取某个 code 关联的所有卡片
    /// </summary>
    public HashSet<string> GetCards(string code)
    {
        return _book.TryGetValue(code, out var cards) ? cards : new HashSet<string>();
    }

    /// <summary>
    /// 获取多个 code 的交集卡片
    /// </summary>
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

    /// <summary>
    /// 获取多个 code 的并集卡片
    /// </summary>
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

    /// <summary>
    /// 添加卡片-标签关联
    /// </summary>
    public void Add(string code, string cardId)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(cardId))
            return;

        AddToMemory(code, cardId);

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO password_book (code, card_id)
            VALUES (@code, @card_id)
        ";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@card_id", cardId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 为一张卡片添加多个 code
    /// </summary>
    public void AddCodesToCard(string cardId, List<string> codes)
    {
        foreach (var code in codes)
        {
            Add(code, cardId);
        }
    }

    /// <summary>
    /// 获取卡片的所有 code
    /// </summary>
    public HashSet<string> GetCodesByCard(string cardId)
    {
        return _reverseBook.TryGetValue(cardId, out var codes) ? codes : new HashSet<string>();
    }

    /// <summary>
    /// 删除卡片的某个 code
    /// </summary>
    public void Remove(string code, string cardId)
    {
        if (_book.TryGetValue(code, out var cards))
        {
            cards.Remove(cardId);
            if (cards.Count == 0)
                _book.Remove(code);
        }

        if (_reverseBook.TryGetValue(cardId, out var codes))
        {
            codes.Remove(code);
            if (codes.Count == 0)
                _reverseBook.Remove(cardId);
        }

        using var conn = _db.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM password_book WHERE code = @code AND card_id = @card_id";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@card_id", cardId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 将 sourceCode 的所有卡片迁移到 targetCode
    /// </summary>
    public void MoveCards(string sourceCode, string targetCode)
    {
        var cards = GetCards(sourceCode);
        if (cards.Count == 0) return;

        // 1. 复制所有卡片到 target
        foreach (var cardId in cards)
        {
            Add(targetCode, cardId);
        }

        // 2. 移除 source 的所有卡片关联
        foreach (var cardId in cards)
        {
            Remove(sourceCode, cardId);
        }

        Console.WriteLine($"[PasswordBook] 已迁移卡片: {sourceCode} → {targetCode}, {cards.Count} 张");
    }

    /// <summary>
    /// 获取所有 code（供外部使用）
    /// </summary>
    public List<string> GetAllCodes()
    {
        return _book.Keys.ToList();
    }

    /// <summary>
    /// 获取卡片数量
    /// </summary>
    public int GetCardCount(string code)
    {
        return _book.TryGetValue(code, out var cards) ? cards.Count : 0;
    }
}
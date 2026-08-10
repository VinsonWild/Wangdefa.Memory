using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine;

/// <summary>
/// 特征推演统一入口
/// </summary>
public class FeatureEngine
{
    private readonly TagDictionary _tagDictionary;
    private readonly PasswordBook _passwordBook;
    private readonly FeatureStats _featureStats;
    private readonly int _pageSize;

    public FeatureEngine(FeatureEngineDb db, int pageSize = 100)
    {
        _tagDictionary = new TagDictionary(db);
        _passwordBook = new PasswordBook(db);
        _featureStats = new FeatureStats(db);
        _tagDictionary.SetPasswordBook(_passwordBook);
        _pageSize = pageSize;
    }

    public TagDictionary Tags => _tagDictionary;
    public PasswordBook Passwords => _passwordBook;
    public FeatureStats Stats => _featureStats;

    /// <summary>
    /// 从用户输入中提取特征标签
    /// </summary>
    public List<string> ExtractCodes(string input)
    {
        var codes = new List<string>();
        var words = SplitWords(input);

        foreach (var word in words)
        {
            var code = _tagDictionary.GetCode(word);
            if (code != null)
            {
                codes.Add(code);
            }
            else
            {
                var entry = _tagDictionary.Add(word, "content", "", "auto");
                codes.Add(entry.Code);
            }
        }

        return codes.Distinct().ToList();
    }

    /// <summary>
    /// 检索：code → 卡片列表 → 扩展 → 循环 → 精选
    /// 支持分页处理，避免内存溢出
    /// </summary>
    public List<FeatureMatchResult> Search(
        List<string> initialCodes,
        int maxDepth = 3,
        int maxCards = 50,
        int topN = 10)
    {
        if (initialCodes == null || initialCodes.Count == 0)
            return new List<FeatureMatchResult>();

        var finalCards = new HashSet<string>();
        var currentCodes = new HashSet<string>(initialCodes);
        var usedCodes = new HashSet<string>(initialCodes);
        var depth = 0;

        var cardMatchCount = new Dictionary<string, int>();

        while (depth < maxDepth && finalCards.Count < maxCards)
        {
            var candidateCards = new HashSet<string>();

            // 分页处理 currentCodes，避免一次性加载过多
            var codeList = currentCodes.ToList();
            for (int i = 0; i < codeList.Count; i += _pageSize)
            {
                var page = codeList.Skip(i).Take(_pageSize);
                foreach (var code in page)
                {
                    var cards = _passwordBook.GetCards(code);
                    foreach (var card in cards)
                    {
                        candidateCards.Add(card);
                        if (!cardMatchCount.ContainsKey(card))
                            cardMatchCount[card] = 0;
                        cardMatchCount[card]++;
                    }

                    // 提前终止：已达 maxCards
                    if (finalCards.Count >= maxCards)
                        break;
                }
                if (finalCards.Count >= maxCards)
                    break;
            }

            var newCards = candidateCards.Except(finalCards).ToList();
            if (newCards.Count == 0)
                break;

            finalCards.UnionWith(candidateCards);
            if (finalCards.Count >= maxCards)
                break;

            // 扩展：从新卡片获取关联 code（分页）
            var newCodes = new HashSet<string>();
            var cardList = candidateCards.Take(_pageSize).ToList();
            foreach (var card in cardList)
            {
                var cardCodes = _passwordBook.GetCodesByCard(card);
                foreach (var code in cardCodes)
                {
                    if (!usedCodes.Contains(code))
                        newCodes.Add(code);
                }
            }

            if (newCodes.Count == 0)
                break;

            currentCodes = newCodes.Take(_pageSize).ToHashSet();
            usedCodes.UnionWith(currentCodes);
            depth++;
        }

        // 结果处理（分页）
        var results = new List<FeatureMatchResult>();
        var cardIds = finalCards.Take(maxCards).ToList();

        foreach (var cardId in cardIds)
        {
            var codes = _passwordBook.GetCodesByCard(cardId).ToList();
            var matchCount = cardMatchCount.TryGetValue(cardId, out var count) ? count : 0;
            var strength = Math.Min(1.0, matchCount / (double)Math.Max(1, initialCodes.Count));

            var matchCodes = usedCodes.Intersect(codes).ToList();
            results.Add(new FeatureMatchResult
            {
                CardId = cardId,
                CardType = GetCardType(cardId),
                Path = GetCardPath(cardId),
                Codes = codes,
                Strength = strength,
                MatchCodes = matchCodes,
                MatchTags = matchCodes
                    .Select(c => _tagDictionary.GetEntryByCode(c)?.Tag ?? c)
                    .ToList()
            });
        }

        return results
            .OrderByDescending(r => r.Strength)
            .ThenByDescending(r => r.Codes.Count)
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// 为卡片打标签（写入密码簿），统一处理标签池 + 密码簿 + 特征统计
    /// </summary>
    /// <param name="cardId">卡片ID</param>
    /// <param name="tags">标签列表</param>
    /// <param name="cardType">卡片类型</param>
    /// <param name="definitions">缺失标签的语义定义（key: tag, value: definition）</param>
    public void TagCard(string cardId, List<string> tags, string cardType = "cognitive", Dictionary<string, string>? definitions = null)
    {
        var codes = new List<string>();

        foreach (var tag in tags)
        {
            var code = _tagDictionary.GetCode(tag);
            if (code == null)
            {
                // 标签不存在：从 definitions 取定义，没有则留空
                var def = definitions?.GetValueOrDefault(tag) ?? "";
                var entry = _tagDictionary.Add(tag, "content", def, "auto");
                code = entry.Code;
                Console.WriteLine($"📝 新标签已创建: {tag} → {code} (定义: {def})");
            }
            else if (definitions != null && definitions.TryGetValue(tag, out var def) && !string.IsNullOrEmpty(def))
            {
                // 标签存在但定义为空：更新定义
                var entry = _tagDictionary.GetEntryByCode(code);
                if (entry != null && string.IsNullOrEmpty(entry.Definition))
                {
                    _tagDictionary.UpdateDefinition(code, def);
                    Console.WriteLine($"📝 标签定义已更新: {tag} → {def}");
                }
            }
            codes.Add(code);
        }

        // 写入密码簿（code → 卡片ID）
        _passwordBook.AddCodesToCard(cardId, codes);

        // 写入特征统计（命中次数）
        _featureStats.RecordHit(codes);

        Console.WriteLine($"✅ TagCard 完成: {cardId}, {codes.Count} 个标签");
    }

    /// <summary>
    /// 获取卡片的全部code
    /// </summary>
    public List<string> GetCardCodes(string cardId)
    {
        return _passwordBook.GetCodesByCard(cardId).ToList();
    }

    private string GetCardPath(string cardId)
    {
        if (cardId.StartsWith("认知_"))
            return $"cognitive/records/{cardId}.json";
        if (cardId.StartsWith("文件_") || cardId.StartsWith("概要_"))
            return $"experience/knowledge/{cardId}.json";
        if (cardId.StartsWith("事件_") || cardId.StartsWith("阅历_"))
            return $"experience/events/{DateTime.Now:yyyy-MM-dd}/{cardId}.json";
        return cardId;
    }

    private string GetCardType(string cardId)
    {
        if (cardId.StartsWith("认知_")) return "cognitive";
        if (cardId.StartsWith("文件_") || cardId.StartsWith("概要_")) return "file";
        if (cardId.StartsWith("事件_") || cardId.StartsWith("阅历_")) return "event";
        return "unknown";
    }

    private List<string> SplitWords(string input)
    {
        var separators = new[] { ' ', '，', '。', '、', '！', '？', ',', '.', '!', '?', '\n', '\r', '\t' };
        var words = input.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        return words.Where(w => w.Length >= 2).ToList();
    }
}
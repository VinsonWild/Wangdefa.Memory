namespace Wangdefa.AgentMemory.FeatureEngine.Models;

/// <summary>
/// 标签池条目
/// </summary>
public class TagEntry
{
    public int TagId { get; set; }
    public string Tag { get; set; } = "";
    public string Code { get; set; } = "";
    public string TagType { get; set; } = "";      // content / relation / scene / task / constraint / intent / skill / special
    public string Definition { get; set; } = "";
    public string Dimensions { get; set; } = "[]"; // JSON数组
    public string RelatedCodes { get; set; } = "[]"; // JSON数组
    public string Synonyms { get; set; } = "[]";   // JSON数组
    public string Source { get; set; } = "auto";   // system / user / auto / ai
    public string Status { get; set; } = "active"; // active / deprecated / merged
    public string? MergedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 密码簿条目
/// </summary>
public class PasswordEntry
{
    public string Code { get; set; } = "";
    public string CardId { get; set; } = "";
    public string CardType { get; set; } = "";     // cognitive / file / event
    public string? TopicId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 特征统计条目
/// </summary>
public class FeatureStat
{
    public string Code { get; set; } = "";
    public int HitCount { get; set; }
    public DateTime? LastHit { get; set; }
    public DateTime FirstSeen { get; set; }
    public int AssociationCount { get; set; }
    public double AvgWeight { get; set; }
}

/// <summary>
/// 特征查询结果
/// </summary>
public class FeatureMatchResult
{
    public string CardId { get; set; } = "";
    public string CardType { get; set; } = "";
    public string Path { get; set; } = "";
    public List<string> Codes { get; set; } = new();
    public double Strength { get; set; }
    public List<string> MatchCodes { get; set; } = new();  // 命中的code
    public List<string> MatchTags { get; set; } = new();   // 命中的标签文本
}
// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

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
/// 标签关联条目 - RelatedCodes JSON 的元素结构
/// </summary>
public class TagRelation
{
    public string Code { get; set; } = "";
    public string Level { get; set; } = RelationLevel.Related;
}

/// <summary>
/// 关联等级常量
/// </summary>
public static class RelationLevel
{
    public const string Synonym = "synonym";
    public const string Related = "related";
    public const string Loose = "loose";
}

/// <summary>
/// 标签相对当前版本的差距（批次 E 版本对齐层用）
/// </summary>
public class TagGap
{
    /// <summary>缺失的字段（需要补）</summary>
    public List<string> Missing { get; set; } = new();

    /// <summary>旧格式字段（需要迁移）</summary>
    public List<string> Legacy { get; set; } = new();

    /// <summary>多余内容（需要删）</summary>
    public List<string> Redundant { get; set; } = new();

    /// <summary>是否为空 gap（即当前版本）</summary>
    public bool IsEmpty => Missing.Count == 0 && Legacy.Count == 0 && Redundant.Count == 0;
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
// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Wangdefa.AgentMemory.FeatureEngine.Models;

namespace Wangdefa.AgentMemory.FeatureEngine.TagServices;

/// <summary>
/// 标签相似度计算 - Jaccard 距离
/// </summary>
public class TagSimilarityService
{
    /// <summary>
    /// 计算两个标签的综合相似度（definition 0.5 + synonyms 0.5）
    /// </summary>
    public double CalculateSimilarity(TagEntry a, TagEntry b)
    {
        if (a == null || b == null) return 0;

        var defSim = JaccardSimilarity(a.Definition ?? "", b.Definition ?? "");
        var synA = JsonSerializer.Deserialize<List<string>>(a.Synonyms ?? "[]") ?? new List<string>();
        var synB = JsonSerializer.Deserialize<List<string>>(b.Synonyms ?? "[]") ?? new List<string>();
        var synSim = JaccardSimilarity(synA, synB);

        return 0.5 * defSim + 0.5 * synSim;
    }

    private double JaccardSimilarity(string textA, string textB)
    {
        if (string.IsNullOrEmpty(textA) || string.IsNullOrEmpty(textB)) return 0;
        var wordsA = Tokenize(textA);
        var wordsB = Tokenize(textB);
        if (wordsA.Count == 0 || wordsB.Count == 0) return 0;
        var intersection = wordsA.Intersect(wordsB).Count();
        var union = wordsA.Union(wordsB).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private double JaccardSimilarity(List<string> listA, List<string> listB)
    {
        if (listA == null || listB == null || listA.Count == 0 || listB.Count == 0) return 0;
        var intersection = listA.Intersect(listB).Count();
        var union = listA.Union(listB).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text)) return new HashSet<string>();

        var separators = new[] { ' ', '，', '。', '、', '；', '：', '！', '？', ',', '.', ';', ':', '!', '?', '\n', '\r', '\t' };
        var words = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        var stopWords = new HashSet<string> { "的", "了", "是", "在", "和", "与", "或", "等", "及", "于", "之", "而", "以", "为", "有", "从", "到", "对", "把", "被", "让", "给", "去", "来", "上", "下", "中", "内", "外", "前", "后", "左", "右" };
        return new HashSet<string>(words.Where(w => w.Length >= 2 && !stopWords.Contains(w)));
    }
}
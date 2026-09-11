// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.FeatureEngine.Models;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Storage;

namespace Wangdefa.AgentMemory.Services;

/// <summary>
/// 标签合并服务：粗筛、合并、ContentTags同步
/// </summary>
public class TagMergeService
{
    private readonly FeatureEngine.FeatureEngine _featureEngine;
    private readonly string _recordsPath;
    private readonly MemoryStore _store;

    public TagMergeService(FeatureEngine.FeatureEngine featureEngine, string recordsPath, MemoryStore store)
    {
        _featureEngine = featureEngine;
        _recordsPath = recordsPath;
        _store = store;
    }

    /// <summary>
    /// 从卡片中筛选需要 LLM 判断的待确认标签
    /// </summary>
    public List<TagEntry> GetTagsToJudge(CognitiveRecordModel card)
    {
        var tagsToJudge = new List<TagEntry>();

        var pendingTagNames = card.Insight?.ContentTags?
            .Where(t => _featureEngine.Tags.GetEntry(t)?.Status == "unexamined")
            .ToList() ?? new List<string>();

        var allPendingTags = _featureEngine.Tags.GetUnexaminedTagsByNames(pendingTagNames);

        foreach (var pending in allPendingTags)
        {
            var candidates = _featureEngine.Tags.FindActiveByName(pending.Tag);
            if (candidates.Count == 0)
            {
                _featureEngine.Tags.Activate(pending.Code);
                Console.WriteLine($"[TagMergeService] 标签已激活（无同名候选）: {pending.Tag}");
                continue;
            }

            double maxScore = 0;
            foreach (var candidate in candidates)
            {
                var score = _featureEngine.Tags.CalculateSimilarity(pending, candidate);
                if (score > maxScore) maxScore = score;
            }

            if (maxScore > 0.3)
            {
                tagsToJudge.Add(pending);
                Console.WriteLine($"[TagMergeService] 标签进入 LLM 判断: {pending.Tag} (相似度: {maxScore:F2})");
            }
            else
            {
                _featureEngine.Tags.Activate(pending.Code);
                Console.WriteLine($"[TagMergeService] 标签已激活（相似度 {maxScore:F2} <= 0.3）: {pending.Tag}");
            }
        }

        return tagsToJudge;
    }

    /// <summary>
    /// 执行标签合并决策
    /// </summary>
    public void ExecuteDecisions(Dictionary<string, string> decisions, List<TagEntry> allPendingTags)
    {
        foreach (var decision in decisions)
        {
            var tagName = decision.Key;
            var action = decision.Value;

            var pendingTag = allPendingTags.FirstOrDefault(t => t.Tag == tagName);
            if (pendingTag == null) continue;

            if (action.StartsWith("merge_to:"))
            {
                var targetTagName = action.Replace("merge_to:", "").Trim();
                var targetTag = _featureEngine.Tags.GetEntry(targetTagName);
                if (targetTag != null)
                {
                    // 1. 合并标签池
                    _featureEngine.Tags.MergeTags(pendingTag.Code, targetTag.Code);

                    // 2. 同步更新所有相关卡片的 ContentTags
                    var cardIds = _featureEngine.Passwords.GetCards(targetTag.Code);
                    foreach (var cid in cardIds)
                    {
                        UpdateCardContentTag(cid, pendingTag.Tag, targetTagName);
                    }

                    Console.WriteLine($"[TagMergeService] 已合并标签: {tagName} → {targetTagName}");
                }
            }
            else if (action == "activate")
            {
                _featureEngine.Tags.Activate(pendingTag.Code);
                Console.WriteLine($"[TagMergeService] 标签已激活: {tagName}");
            }
            else if (action == "discard")
            {
                // 泛用词，跳过激活，不写标签池（保守处理，不误伤历史数据）
                Console.WriteLine($"[TagMergeService] 标签被判定为泛用，跳过激活: {tagName}");
            }
        }
    }

    /// <summary>
    /// 更新卡片的 ContentTags（走 MemoryStore 原子写入）
    /// </summary>
    private async void UpdateCardContentTag(string cardId, string oldTag, string newTag)
    {
        var card = await _store.ReadCardAsync(cardId);
        if (card?.Insight?.ContentTags == null) return;

        var tags = card.Insight.ContentTags.ToList();
        var index = tags.IndexOf(oldTag);
        if (index >= 0)
        {
            tags[index] = newTag;
            card.Insight.ContentTags = tags.ToArray();

            await _store.WriteCardAsync(cardId, card);
            Console.WriteLine($"[TagMergeService] 卡片 {cardId} ContentTags 已更新: {oldTag} → {newTag}");
        }
    }
}
// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Storage;

/// <summary>
/// 记忆体统一存储 - 所有文件读写操作收敛到此
/// </summary>
public class MemoryStore
{
    private readonly string _basePath;
    private readonly string _recordsPath;
    private readonly string _knowledgePath;
    private readonly string _thinkingPath;
    private readonly string _eventsPath;

    public MemoryStore(string basePath)
    {
        _basePath = basePath;
        _recordsPath = Path.Combine(basePath, "cognitive", "records");
        _knowledgePath = Path.Combine(basePath, "experience", "knowledge");
        _thinkingPath = Path.Combine(basePath, "thinking", "chat");
        _eventsPath = Path.Combine(basePath, "experience", "events");
    }

    // ============================================================
    // 卡片读写
    // ============================================================

    public async Task WriteCardAsync(string cardId, CognitiveRecordModel card)
    {
        var path = Path.Combine(_recordsPath, $"{cardId}.json");
        await WriteJsonAsync(path, card);
    }

    public async Task<CognitiveRecordModel?> ReadCardAsync(string cardId)
    {
        var path = Path.Combine(_recordsPath, $"{cardId}.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<CognitiveRecordModel>(json);
    }

    public async Task<bool> CardExistsAsync(string cardId)
    {
        var path = Path.Combine(_recordsPath, $"{cardId}.json");
        return File.Exists(path);
    }

    // ============================================================
    // 索引读写
    // ============================================================

    public async Task WriteIndexAsync(string indexId, DiversionIndexModel index, string topicId)
    {
        var topicPath = Path.Combine(_thinkingPath, SanitizeTopicId(topicId));
        Directory.CreateDirectory(topicPath);
        var path = Path.Combine(topicPath, $"{indexId}.json");
        await WriteJsonAsync(path, index);
    }

    public async Task<DiversionIndexModel?> ReadIndexAsync(string indexId, string topicId)
    {
        var topicPath = Path.Combine(_thinkingPath, SanitizeTopicId(topicId));
        var path = Path.Combine(topicPath, $"{indexId}.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<DiversionIndexModel>(json);
    }

    public async Task<string[]?> GetIndexFilesAsync(string topicId)
    {
        var topicPath = Path.Combine(_thinkingPath, SanitizeTopicId(topicId));
        if (!Directory.Exists(topicPath)) return null;
        return Directory.GetFiles(topicPath, "记录_*.json");
    }

    // ============================================================
    // 概览读写
    // ============================================================

    public async Task<string> WriteOverviewAsync(string topicId, string cardId, string overviewText)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var overviewId = $"概览_{timestamp}";
        var topicPath = Path.Combine(_knowledgePath, SanitizeTopicId(topicId));
        Directory.CreateDirectory(topicPath);

        var overview = new OverviewModel
        {
            Id = overviewId,
            TopicId = topicId,
            CognitiveRecordId = cardId,
            Text = overviewText,
            ContentType = "chat",
            WordCount = overviewText.Length,
            Confidence = 0.8,
            CreatedAt = DateTime.Now
        };

        var path = Path.Combine(topicPath, $"{overviewId}.json");
        await WriteJsonAsync(path, overview);

        Console.WriteLine($"✅ 概览已保存: {path}");
        return Path.Combine(topicId, $"{overviewId}.json");
    }

    public async Task<OverviewModel?> ReadOverviewAsync(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath)) return null;

        var fullPath = Path.Combine(_knowledgePath, sourcePath);
        if (!File.Exists(fullPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(fullPath);
            return JsonSerializer.Deserialize<OverviewModel>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> ReadOverviewTextAsync(string sourcePath)
    {
        var overview = await ReadOverviewAsync(sourcePath);
        return overview?.Text;
    }

    // ============================================================
    // 摘要读写
    // ============================================================

    public async Task<string> WriteSummaryAsync(string topicId, SummaryModel summary)
    {
        var topicPath = Path.Combine(_knowledgePath, SanitizeTopicId(topicId));
        Directory.CreateDirectory(topicPath);
        var path = Path.Combine(topicPath, $"{summary.Id}.json");
        await WriteJsonAsync(path, summary);
        return Path.Combine(topicId, $"{summary.Id}.json");
    }

    public async Task<SummaryModel?> ReadSummaryAsync(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath)) return null;

        var fullPath = Path.Combine(_knowledgePath, sourcePath);
        if (!File.Exists(fullPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(fullPath);
            return JsonSerializer.Deserialize<SummaryModel>(json);
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // 事件读写
    // ============================================================

    public async Task WriteEventAsync(EventModel evt)
    {
        var dayPath = Path.Combine(_eventsPath, evt.Timestamp.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dayPath);
        var path = Path.Combine(dayPath, $"{evt.EventId}.json");
        await WriteJsonAsync(path, evt);
    }

    public async Task<EventModel?> ReadEventAsync(string eventId)
    {
        if (!Directory.Exists(_eventsPath)) return null;

        foreach (var dir in Directory.GetDirectories(_eventsPath))
        {
            var path = Path.Combine(dir, $"{eventId}.json");
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<EventModel>(json);
            }
        }
        return null;
    }

    public async Task<List<EventModel>> GetDayEventsAsync(DateTime date)
    {
        var dayPath = Path.Combine(_eventsPath, date.ToString("yyyy-MM-dd"));
        if (!Directory.Exists(dayPath)) return new List<EventModel>();

        var events = new List<EventModel>();
        foreach (var file in Directory.GetFiles(dayPath, "事件_*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var evt = JsonSerializer.Deserialize<EventModel>(json);
                if (evt != null) events.Add(evt);
            }
            catch { /* 跳过损坏文件 */ }
        }
        return events.OrderBy(e => e.Timestamp).ToList();
    }

    // ============================================================
    // 卡片 ContentTags 更新
    // ============================================================

    public async Task UpdateCardContentTagsAsync(string cardId, string[] tags)
    {
        var card = await ReadCardAsync(cardId);
        if (card?.Insight == null) return;
        card.Insight.ContentTags = tags;
        await WriteCardAsync(cardId, card);
    }

    // ============================================================
    // 删除操作
    // ============================================================

    public void DeleteCard(string cardId)
    {
        var path = Path.Combine(_recordsPath, $"{cardId}.json");
        if (File.Exists(path)) File.Delete(path);
    }

    public void DeleteOverview(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath)) return;
        var fullPath = Path.Combine(_knowledgePath, sourcePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    // ============================================================
    // 原子写入核心
    // ============================================================

    private async Task WriteJsonAsync<T>(string path, T data)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    // ============================================================
    // 工具方法
    // ============================================================

    private string SanitizeTopicId(string topicId)
    {
        if (string.IsNullOrEmpty(topicId)) return "default";
        foreach (var c in Path.GetInvalidFileNameChars())
            topicId = topicId.Replace(c, '_');
        return topicId;
    }
}
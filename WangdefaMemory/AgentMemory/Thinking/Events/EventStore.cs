using System.Text.Json;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Thinking.Events;

/// <summary>
/// 事件存储 — 按日期分目录，一个事件一个文件
/// </summary>
public class EventStore : IEventStore
{
    private readonly string _basePath;

    public EventStore(string memoryBasePath)
    {
        _basePath = Path.Combine(memoryBasePath, "experience", "events");
        Directory.CreateDirectory(_basePath);
    }

    private string GetDayPath(DateTime date)
    {
        var path = Path.Combine(_basePath, date.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(path);
        return path;
    }

    private string GetEventFilePath(DateTime date, string eventId)
    {
        return Path.Combine(GetDayPath(date), $"{eventId}.json");
    }

    /// <summary>
    /// 保存事件
    /// </summary>
    public async Task SaveAsync(EventModel evt)
    {
        var path = GetEventFilePath(evt.Timestamp, evt.EventId);
        var content = JsonSerializer.Serialize(evt, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, content);
    }

    /// <summary>
    /// 加载单个事件
    /// </summary>
    public async Task<EventModel?> LoadAsync(string eventId, DateTime? date = null)
    {
        if (date == null)
        {
            var dirs = Directory.GetDirectories(_basePath);
            foreach (var dir in dirs)
            {
                var path = Path.Combine(dir, $"{eventId}.json");
                if (File.Exists(path))
                {
                    var fileContent = await File.ReadAllTextAsync(path);
                    return JsonSerializer.Deserialize<EventModel>(fileContent);
                }
            }
            return null;
        }

        var dayPath = GetDayPath(date.Value);
        var filePath = Path.Combine(dayPath, $"{eventId}.json");
        if (!File.Exists(filePath)) return null;

        var jsonContent = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<EventModel>(jsonContent);
    }

    /// <summary>
    /// 获取某天的所有事件
    /// </summary>
    public async Task<List<EventModel>> GetDayEventsAsync(DateTime date)
    {
        var dayPath = GetDayPath(date);
        if (!Directory.Exists(dayPath)) return new List<EventModel>();

        var files = Directory.GetFiles(dayPath, "事件_*.json");
        var events = new List<EventModel>();

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var evt = JsonSerializer.Deserialize<EventModel>(content);
                if (evt != null) events.Add(evt);
            }
            catch { /* 跳过损坏文件 */ }
        }

        return events.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// 获取某天的事件概览（仅摘要信息，不加载完整内容）
    /// </summary>
    public async Task<List<EventSummary>> GetDaySummariesAsync(DateTime date)
    {
        var dayPath = GetDayPath(date);
        if (!Directory.Exists(dayPath)) return new List<EventSummary>();

        var files = Directory.GetFiles(dayPath, "事件_*.json");
        var summaries = new List<EventSummary>();

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                summaries.Add(new EventSummary
                {
                    EventId = root.GetProperty("EventId").GetString() ?? "",
                    EventType = root.GetProperty("EventType").GetString() ?? "",
                    EventLevel = root.GetProperty("EventLevel").GetString() ?? "",
                    Timestamp = root.GetProperty("Timestamp").GetDateTime(),
                    FeatureTags = root.GetProperty("FeatureTags").Deserialize<string[]>() ?? Array.Empty<string>(),
                    Summary = root.GetProperty("Result").GetProperty("Summary").GetString() ?? ""
                });
            }
            catch { /* 跳过损坏文件 */ }
        }

        return summaries.OrderBy(s => s.Timestamp).ToList();
    }

    /// <summary>
    /// 获取父事件下的所有子步骤
    /// </summary>
    public async Task<List<EventModel>> GetStepsAsync(string parentEventId)
    {
        var allEvents = new List<EventModel>();
        var dirs = Directory.GetDirectories(_basePath);

        foreach (var dir in dirs)
        {
            var files = Directory.GetFiles(dir, "事件_*.json");
            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var evt = JsonSerializer.Deserialize<EventModel>(content);
                    if (evt?.ParentEventId == parentEventId)
                        allEvents.Add(evt);
                }
                catch { }
            }
        }

        return allEvents.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// 回写提炼结果到事件
    /// </summary>
    public async Task UpdateInsightAsync(string eventId, DialogueAnalysis insight)
    {
        var dirs = Directory.GetDirectories(_basePath);
        foreach (var dir in dirs)
        {
            var path = Path.Combine(dir, $"{eventId}.json");
            if (File.Exists(path))
            {
                var content = await File.ReadAllTextAsync(path);
                var evt = JsonSerializer.Deserialize<EventModel>(content);
                if (evt != null)
                {
                    evt.ExtractedInsight = insight;
                    var updatedContent = JsonSerializer.Serialize(evt, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(path, updatedContent);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 删除某天的所有事件
    /// </summary>
    public void DeleteDay(DateTime date)
    {
        var dayPath = GetDayPath(date);
        if (Directory.Exists(dayPath))
            Directory.Delete(dayPath, true);
    }
}

public class EventSummary
{
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string EventLevel { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string[] FeatureTags { get; set; } = Array.Empty<string>();
    public string Summary { get; set; } = "";
}
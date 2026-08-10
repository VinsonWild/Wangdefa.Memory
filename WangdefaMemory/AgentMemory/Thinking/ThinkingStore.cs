using System.Text.Json;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Thinking.Events;

namespace Wangdefa.AgentMemory.Thinking;

/// <summary>
/// 思考层存储 - 分流索引读写（按话题分目录）
/// 不再存对话原文，只存"去哪找"的索引
/// </summary>
public class ThinkingStore : IThinkingStore
{
    private readonly string _basePath;
    private readonly IEventStore _eventStore;

    public ThinkingStore(string basePath, IEventStore eventStore)
    {
        _basePath = basePath;
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <summary>
    /// 获取话题存储路径
    /// </summary>
    public string GetTopicPath(string topicId)
    {
        var safeTopic = SanitizeTopicId(topicId);
        var path = Path.Combine(_basePath, "thinking", "chat", safeTopic);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 【兼容】获取对话存储路径（调用 GetTopicPath）
    /// </summary>
    public string GetChatPath(string topicId) => GetTopicPath(topicId);

    /// <summary>
    /// 保存分流索引（替代原来的 Save）
    /// </summary>
    public async Task<string> SaveIndex(DiversionIndexModel index, string topicId = "default")
    {
        var recordId = $"记录_{DateTime.Now:yyyyMMdd_HHmmss}";
        var chatPath = GetTopicPath(topicId);
        var path = Path.Combine(chatPath, $"{recordId}.json");
        var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        return recordId;
    }

    /// <summary>
    /// 加载分流索引（替代原来的 Load）
    /// </summary>
    public async Task<DiversionIndexModel?> LoadIndex(string recordId, string? topicId = null)
    {
        // 1. 如果有 topicId，先查新路径
        if (!string.IsNullOrEmpty(topicId))
        {
            var chatPath = GetTopicPath(topicId);
            var path = Path.Combine(chatPath, $"{recordId}.json");
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<DiversionIndexModel>(json);
            }
        }

        // 2. 回退到旧路径（兼容历史数据）
        var oldPath = Path.Combine(_basePath, "thinking", "records", $"{recordId}.json");
        if (File.Exists(oldPath))
        {
            var json = await File.ReadAllTextAsync(oldPath);
            try
            {
                var oldRecord = JsonSerializer.Deserialize<ChatRecord>(json);
                if (oldRecord != null)
                {
                    return new DiversionIndexModel
                    {
                        CognitiveRecordId = recordId,
                        EventType = "chat",
                        TopicId = oldRecord.TopicId ?? topicId ?? "default",
                        SummaryPointer = "",
                        OverviewPointer = "",
                        FullTextPointer = "",
                        FullTextType = "db",
                        CreatedAt = oldRecord.CreatedAt,
                        LastAccessAt = oldRecord.CreatedAt
                    };
                }
            }
            catch
            {
                // 转换失败，返回空
            }
        }

        return null;
    }

    /// <summary>
    /// 获取最新一条记录ID
    /// </summary>
    public async Task<string?> GetLatestRecordId(string topicId)
    {
        var chatPath = GetTopicPath(topicId);
        if (!Directory.Exists(chatPath)) return null;

        var files = Directory.GetFiles(chatPath, "记录_*.json");
        if (files.Length == 0) return null;

        var latest = files.OrderBy(f => f).LastOrDefault();
        if (string.IsNullOrEmpty(latest)) return null;

        return Path.GetFileNameWithoutExtension(latest);
    }

    /// <summary>
    /// 更新已有索引
    /// </summary>
    public async Task UpdateIndex(string recordId, string topicId, DiversionIndexModel index)
    {
        var chatPath = GetTopicPath(topicId);
        var path = Path.Combine(chatPath, $"{recordId}.json");
        var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// 更新已有记录（兼容旧调用）
    /// </summary>
    public async Task Update(string recordId, string topicId, object record)
    {
        if (record is DiversionIndexModel index)
        {
            await UpdateIndex(recordId, topicId, index);
            return;
        }

        var chatPath = GetTopicPath(topicId);
        var path = Path.Combine(chatPath, $"{recordId}.json");
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// 【兼容保留】加载为强类型 ChatRecord（仅用于反馈判断，读取旧数据）
    /// </summary>
    public async Task<ChatRecord?> LoadChatRecord(string recordId, string topicId)
    {
        var chatPath = GetTopicPath(topicId);
        var path = Path.Combine(chatPath, $"{recordId}.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<ChatRecord>(json);
    }

    /// <summary>
    /// 从事件存储加载事件
    /// </summary>
    public async Task<EventModel?> LoadEvent(string eventId)
    {
        return await _eventStore.LoadAsync(eventId);
    }

    private string SanitizeTopicId(string topicId)
    {
        if (string.IsNullOrEmpty(topicId)) return "default";
        foreach (var c in Path.GetInvalidFileNameChars())
            topicId = topicId.Replace(c, '_');
        return topicId;
    }
}
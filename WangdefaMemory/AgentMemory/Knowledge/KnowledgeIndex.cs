using System.Text.Json;

namespace Wangdefa.AgentMemory.Knowledge;

/// <summary>
/// 知识索引 - 按话题存储知识单元的索引
/// </summary>
public class KnowledgeIndex
{
    private readonly string _basePath;

    public KnowledgeIndex(string basePath)
    {
        _basePath = basePath;
    }

    private string GetIndexPath(string topicId)
    {
        var safeTopic = SanitizeTopicId(topicId);
        return Path.Combine(_basePath, "thinking", "knowledge", safeTopic, "index.json");
    }

    public async Task<KnowledgeIndexData> Load(string topicId)
    {
        var path = GetIndexPath(topicId);
        if (!File.Exists(path))
        {
            return new KnowledgeIndexData { TopicId = topicId, KnowledgeUnits = new List<KnowledgeIndexEntry>() };
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<KnowledgeIndexData>(json) ?? new KnowledgeIndexData { TopicId = topicId, KnowledgeUnits = new List<KnowledgeIndexEntry>() };
        }
        catch
        {
            return new KnowledgeIndexData { TopicId = topicId, KnowledgeUnits = new List<KnowledgeIndexEntry>() };
        }
    }

    public async Task AddEntry(string topicId, string knowledgeId, string[] tags, string summary, double weight)
    {
        var data = await Load(topicId);
        data.KnowledgeUnits.RemoveAll(x => x.Id == knowledgeId);
        data.KnowledgeUnits.Add(new KnowledgeIndexEntry
        {
            Id = knowledgeId,
            Tags = tags,
            Summary = summary,
            Weight = weight,
            CreatedAt = DateTime.Now
        });
        await Save(topicId, data);
    }

    public async Task<List<KnowledgeIndexEntry>> Match(string topicId, string[] queryTags)
    {
        var data = await Load(topicId);
        if (data.KnowledgeUnits.Count == 0) return new List<KnowledgeIndexEntry>();

        return data.KnowledgeUnits
            .Where(k => queryTags.Any(tag => k.Tags.Contains(tag)))
            .OrderByDescending(k => k.Weight)
            .ToList();
    }

    /// <summary>
    /// 保存索引数据（公开方法，供 KnowledgeStore 调用）
    /// </summary>
    public async Task Save(string topicId, KnowledgeIndexData data)
    {
        var path = GetIndexPath(topicId);
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private string SanitizeTopicId(string topicId)
    {
        if (string.IsNullOrEmpty(topicId)) return "default";
        foreach (var c in Path.GetInvalidFileNameChars())
            topicId = topicId.Replace(c, '_');
        return topicId;
    }
}

public class KnowledgeIndexData
{
    public string TopicId { get; set; } = "";
    public List<KnowledgeIndexEntry> KnowledgeUnits { get; set; } = new();
}

public class KnowledgeIndexEntry
{
    public string Id { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Summary { get; set; } = "";
    public double Weight { get; set; } = 0.5;
    public DateTime CreatedAt { get; set; }
}
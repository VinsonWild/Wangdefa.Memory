using System.Text.Json;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Knowledge;

/// <summary>
/// 知识详情存储 - 按话题存储对话分析结果（行为/偏好/决策）
/// </summary>
public class KnowledgeStore : IKnowledgeStore
{
    private readonly string _basePath;
    private readonly KnowledgeIndex _knowledgeIndex;

    public KnowledgeStore(string basePath)
    {
        _basePath = basePath;
        _knowledgeIndex = new KnowledgeIndex(basePath);
    }

    private string GetKnowledgePath(string topicId)
    {
        var safeTopic = SanitizeTopicId(topicId);
        var path = Path.Combine(_basePath, "experience", "knowledge", safeTopic);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 保存对话分析结果（从 object 提取字段），并更新索引
    /// </summary>
    public async Task<string> Save(object analysis, string topicId)
    {
        var id = $"分析_{DateTime.Now:yyyyMMdd_HHmmss}";
        var path = Path.Combine(GetKnowledgePath(topicId), $"{id}.json");
        var json = JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);

        var tags = ExtractTags(analysis);
        var summary = ExtractSummary(analysis);
        var weight = ExtractWeight(analysis);

        await _knowledgeIndex.AddEntry(topicId, id, tags, summary, weight);

        return id;
    }

    /// <summary>
    /// 保存对话分析结果（强类型 DialogueAnalysis），并更新索引
    /// </summary>
    public async Task<string> SaveDialogueAnalysis(DialogueAnalysis analysis, string topicId)
    {
        var id = analysis.Id;
        var path = Path.Combine(GetKnowledgePath(topicId), $"{id}.json");
        var json = JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);

        await _knowledgeIndex.AddEntry(topicId, id, analysis.Tags, analysis.Summary, analysis.Weight);

        return id;
    }

    public async Task<object?> Load(string id, string topicId)
    {
        var path = Path.Combine(GetKnowledgePath(topicId), $"{id}.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<object>(json);
    }

    /// <summary>
    /// 加载强类型对话分析结果
    /// </summary>
    public async Task<DialogueAnalysis?> LoadDialogueAnalysis(string id, string topicId)
    {
        var path = Path.Combine(GetKnowledgePath(topicId), $"{id}.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<DialogueAnalysis>(json);
    }

    /// <summary>
    /// 按标签匹配对话分析结果
    /// </summary>
    public async Task<List<KnowledgeIndexEntry>> Search(string topicId, string[] queryTags)
    {
        if (queryTags == null || queryTags.Length == 0)
            return new List<KnowledgeIndexEntry>();
        return await _knowledgeIndex.Match(topicId, queryTags);
    }

    /// <summary>
    /// 获取某个话题下的所有对话分析结果
    /// </summary>
    public async Task<List<KnowledgeIndexEntry>> GetAll(string topicId)
    {
        var data = await _knowledgeIndex.Load(topicId);
        return data.KnowledgeUnits.OrderByDescending(x => x.Weight).ToList();
    }

    /// <summary>
    /// 删除对话分析结果（同时删除索引和文件）
    /// </summary>
    public async Task<bool> Delete(string id, string topicId)
    {
        var path = Path.Combine(GetKnowledgePath(topicId), $"{id}.json");
        if (!File.Exists(path)) return false;

        File.Delete(path);

        var data = await _knowledgeIndex.Load(topicId);
        var removed = data.KnowledgeUnits.RemoveAll(x => x.Id == id);
        if (removed > 0)
        {
            await _knowledgeIndex.Save(topicId, data);
        }
        return removed > 0;
    }

    /// <summary>
    /// 重建知识索引（从所有对话分析文件重新生成索引）
    /// </summary>
    public async Task RebuildIndex(string topicId)
    {
        var knowledgePath = GetKnowledgePath(topicId);
        if (!Directory.Exists(knowledgePath)) return;

        var index = new Dictionary<string, List<string>>();
        var files = Directory.GetFiles(knowledgePath, "分析_*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonDocument.Parse(json);
                var id = Path.GetFileNameWithoutExtension(file);

                // 提取 tags
                if (doc.RootElement.TryGetProperty("Tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tags.EnumerateArray())
                    {
                        var tagStr = tag.GetString() ?? "";
                        if (string.IsNullOrEmpty(tagStr)) continue;
                        if (!index.ContainsKey(tagStr))
                            index[tagStr] = new List<string>();
                        if (!index[tagStr].Contains(id))
                            index[tagStr].Add(id);
                    }
                }

                // 兼容旧格式 "tags"（小写）
                if (doc.RootElement.TryGetProperty("tags", out var tagsLower) && tagsLower.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tagsLower.EnumerateArray())
                    {
                        var tagStr = tag.GetString() ?? "";
                        if (string.IsNullOrEmpty(tagStr)) continue;
                        if (!index.ContainsKey(tagStr))
                            index[tagStr] = new List<string>();
                        if (!index[tagStr].Contains(id))
                            index[tagStr].Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KnowledgeStore] 重建索引失败: {file}, {ex.Message}");
            }
        }

        var indexPath = Path.Combine(knowledgePath, "index.json");
        var jsonOutput = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(indexPath, jsonOutput);
        Console.WriteLine($"[KnowledgeStore] 知识索引已重建: {topicId}, {index.Count} 个标签");
    }

    private string[] ExtractTags(object analysis)
    {
        var props = analysis.GetType().GetProperties();
        var tagsProp = props.FirstOrDefault(p => p.Name == "tags" || p.Name == "Tags");
        if (tagsProp != null)
        {
            var value = tagsProp.GetValue(analysis);
            if (value is string[] arr) return arr;
            if (value is string str) return str.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }
        return Array.Empty<string>();
    }

    private string ExtractSummary(object analysis)
    {
        var props = analysis.GetType().GetProperties();
        var summaryProp = props.FirstOrDefault(p => p.Name == "summary" || p.Name == "Summary");
        if (summaryProp != null)
        {
            var value = summaryProp.GetValue(analysis)?.ToString();
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return "";
    }

    private double ExtractWeight(object analysis)
    {
        var props = analysis.GetType().GetProperties();
        var weightProp = props.FirstOrDefault(p => p.Name == "weight" || p.Name == "Weight" || p.Name == "confidence" || p.Name == "Confidence");
        if (weightProp != null)
        {
            var value = weightProp.GetValue(analysis);
            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is decimal m) return (double)m;
        }
        return 0.5;
    }

    private string SanitizeTopicId(string topicId)
    {
        if (string.IsNullOrEmpty(topicId)) return "default";
        foreach (var c in Path.GetInvalidFileNameChars())
            topicId = topicId.Replace(c, '_');
        return topicId;
    }
}
using System.Text.Json;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Thinking;

namespace Wangdefa.AgentMemory.Signal;

/// <summary>
/// 记忆合并器 - 将旧对话记录打包成归档文件
/// </summary>
public class MemoryMerger
{
    private readonly IThinkingStore _thinkingStore;
    private readonly string _basePath;

    public MemoryMerger(string basePath, IThinkingStore thinkingStore)
    {
        _basePath = basePath;
        _thinkingStore = thinkingStore;
    }

    /// <summary>
    /// 合并指定话题下超过指定天数的记录
    /// </summary>
    public async Task<int> MergeTopic(string topicId, int daysThreshold = 90)
    {
        var chatPath = _thinkingStore.GetChatPath(topicId);
        if (!Directory.Exists(chatPath)) return 0;

        var cutoff = DateTime.Now.AddDays(-daysThreshold);
        var files = Directory.GetFiles(chatPath, "记录_*.json")
            .Select(f => new
            {
                Path = f,
                Record = JsonSerializer.Deserialize<ChatRecord>(File.ReadAllText(f))
            })
            .Where(x => x.Record != null && x.Record.CreatedAt < cutoff)
            .OrderBy(x => x.Record!.CreatedAt)
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine($"📭 话题 {topicId} 没有需要合并的记录（{daysThreshold}天前）");
            return 0;
        }

        // 生成归档文件名
        var period = $"{DateTime.Now.Year}Q{(DateTime.Now.Month - 1) / 3 + 1}";
        var archivePath = Path.Combine(chatPath, $"archive_{period}.json");

        // 读取已有归档
        ArchiveFile? archive = null;
        if (File.Exists(archivePath))
        {
            var json = await File.ReadAllTextAsync(archivePath);
            archive = JsonSerializer.Deserialize<ArchiveFile>(json);
        }

        if (archive == null)
        {
            archive = new ArchiveFile
            {
                TopicId = topicId,
                Period = period,
                MergedAt = DateTime.Now,
                Records = new List<ChatRecord>()
            };
        }

        // 把旧记录加入归档
        var mergedCount = 0;
        foreach (var file in files)
        {
            if (file.Record != null)
            {
                archive.Records.Add(file.Record);
                mergedCount++;
                // 软删除：改为 .bak 扩展名
                var bakPath = file.Path + ".bak";
                if (File.Exists(bakPath))
                    File.Delete(bakPath);
                File.Move(file.Path, bakPath);
            }
        }

        // 写回归档文件
        archive.MergedAt = DateTime.Now;
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(archivePath, JsonSerializer.Serialize(archive, options));

        Console.WriteLine($"✅ 话题 {topicId} 已合并 {mergedCount} 条记录到 {archivePath}");
        return mergedCount;
    }

    /// <summary>
    /// 合并所有话题
    /// </summary>
    public async Task<int> MergeAllTopics(int daysThreshold = 90)
    {
        var thinkingPath = Path.Combine(_basePath, "thinking", "chat");
        if (!Directory.Exists(thinkingPath)) return 0;

        var topicDirs = Directory.GetDirectories(thinkingPath);
        var total = 0;

        foreach (var dir in topicDirs)
        {
            var topicId = Path.GetFileName(dir);
            total += await MergeTopic(topicId, daysThreshold);
        }

        Console.WriteLine($"✅ 全部合并完成，共处理 {total} 条记录");
        return total;
    }

    /// <summary>
    /// 清理备份文件（确认合并无误后调用）
    /// </summary>
    public void CleanBackups(string topicId)
    {
        var chatPath = _thinkingStore.GetChatPath(topicId);
        if (!Directory.Exists(chatPath)) return;

        var bakFiles = Directory.GetFiles(chatPath, "*.bak");
        foreach (var file in bakFiles)
        {
            File.Delete(file);
            Console.WriteLine($"🗑️ 已删除备份: {Path.GetFileName(file)}");
        }
    }
}
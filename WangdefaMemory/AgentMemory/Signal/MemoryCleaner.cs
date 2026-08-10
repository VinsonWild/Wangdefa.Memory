using System.Text.Json;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Signal;

public class MemoryCleaner
{
    private readonly string _cognitivePath;
    private readonly string _thinkingPath;
    private readonly double _minWeight;
    private readonly int _minAgeDays;
    private readonly int _batchSize;

    public MemoryCleaner(
        string basePath = "memory",
        double minWeight = 0.3,
        int minAgeDays = 30,
        int batchSize = 100)
    {
        _cognitivePath = Path.Combine(basePath, "cognitive", "records");
        _thinkingPath = Path.Combine(basePath, "thinking");
        _minWeight = minWeight;
        _minAgeDays = minAgeDays;
        _batchSize = batchSize;
    }

    /// <summary>
    /// 执行清理，返回清理数量
    /// </summary>
    public async Task<int> CleanAsync()
    {
        var cutoffDate = DateTime.Now.AddDays(-_minAgeDays);
        var cleaned = 0;
        var allRecords = new List<CognitiveRecordModel>();

        // ===== 1. 加载所有权重记录 =====
        if (Directory.Exists(_cognitivePath))
        {
            var files = Directory.GetFiles(_cognitivePath, "认知_*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var record = JsonSerializer.Deserialize<CognitiveRecordModel>(json);
                    if (record != null)
                    {
                        allRecords.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MemoryCleaner] 加载失败: {Path.GetFileName(file)}, {ex.Message}");
                }
            }
        }

        if (allRecords.Count == 0)
        {
            Console.WriteLine("[MemoryCleaner] 没有找到认知记录");
            return 0;
        }

        Console.WriteLine($"[MemoryCleaner] 加载了 {allRecords.Count} 条认知记录");

        // ===== 2. 批量重算所有权重 =====
        Console.WriteLine("[MemoryCleaner] 开始批量重算权重...");
        foreach (var record in allRecords)
        {
            record.Weight = WeightCalculator.Calculate(record.CreatedAt, record.LastAccessAt);
            await SaveCognitiveRecord(record);
        }
        Console.WriteLine("[MemoryCleaner] 权重重算完成");

        // ===== 3. 找出需要清理的记录 =====
        var toClean = allRecords
            .Where(r => r.Weight < _minWeight && r.CreatedAt < cutoffDate)
            .ToList();

        Console.WriteLine($"[MemoryCleaner] 找到 {toClean.Count} 条待清理记录（权重 < {_minWeight} 且超过 {_minAgeDays} 天）");

        if (toClean.Count == 0)
        {
            Console.WriteLine("[MemoryCleaner] 没有需要清理的记录");
            return 0;
        }

        // ===== 4. 执行清理 =====
        foreach (var record in toClean.Take(_batchSize))
        {
            var recordPath = Path.Combine(_cognitivePath, $"{record.Id}.json");
            await ArchiveRecord(recordPath, record.Id, "认知");
            cleaned++;
        }

        // ===== 5. 关联清理思考层记录 =====
        if (Directory.Exists(_thinkingPath))
        {
            var chatDirs = Directory.GetDirectories(Path.Combine(_thinkingPath, "chat"));
            foreach (var dir in chatDirs)
            {
                if (cleaned >= _batchSize) break;

                var files = Directory.GetFiles(dir, "记录_*.json");
                foreach (var file in files)
                {
                    if (cleaned >= _batchSize) break;

                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var chatRecord = JsonSerializer.Deserialize<ChatRecord>(json);
                        if (chatRecord == null) continue;

                        // 检查是否关联到已清理的认知记录
                        var shouldClean = toClean.Any(r => r.RecordId == Path.GetFileNameWithoutExtension(file));
                        if (shouldClean)
                        {
                            await ArchiveRecord(file, Path.GetFileNameWithoutExtension(file), "对话");
                            cleaned++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MemoryCleaner] 处理失败: {Path.GetFileName(file)}, {ex.Message}");
                    }
                }
            }
        }

        Console.WriteLine($"[MemoryCleaner] 清理完成，共 {cleaned} 条记录");
        return cleaned;
    }

    private async Task ArchiveRecord(string filePath, string id, string type)
    {
        var archiveDir = Path.Combine(Path.GetDirectoryName(filePath)!, "archive");
        Directory.CreateDirectory(archiveDir);

        var destPath = Path.Combine(archiveDir, $"{id}.bak");
        if (File.Exists(destPath))
            File.Delete(destPath);
        File.Move(filePath, destPath);
        Console.WriteLine($"[MemoryCleaner] 已归档: {type} {id}");
        await Task.CompletedTask;
    }

    private async Task SaveCognitiveRecord(CognitiveRecordModel record)
    {
        var path = Path.Combine(_cognitivePath, $"{record.Id}.json");
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
}
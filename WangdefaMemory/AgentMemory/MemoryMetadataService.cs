using System.Text.Json;

namespace Wangdefa.AgentMemory;

public class MemoryMetadataService
{
    private readonly string _knowledgePath;

    public MemoryMetadataService(string basePath)
    {
        _knowledgePath = Path.Combine(basePath, "experience", "knowledge");
    }

    public async Task SaveMetadataAsync(
        string topicId,
        string sourcePath,
        string sourceType,
        string fileName,
        long fileSize,
        string fileHash,
        string mimeType = "",
        string status = "pending")
    {
        var metadata = new
        {
            topic_id = topicId,
            source_path = sourcePath,
            source_type = sourceType,
            file_name = fileName,
            file_size = fileSize,
            file_hash = fileHash,
            mime_type = mimeType,
            status = status,
            created_at = DateTime.Now
        };

        var path = Path.Combine(_knowledgePath, topicId, $"元数据_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task<string> GetSourcePathAsync(string topicId, string recordId)
    {
        var metadataDir = Path.Combine(_knowledgePath, topicId);
        if (!Directory.Exists(metadataDir)) return "";

        foreach (var file in Directory.GetFiles(metadataDir, "元数据_*.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("source_path", out var path))
                return path.GetString() ?? "";
        }
        return "";
    }

    public async Task UpdateMetadataStatusAsync(string topicId, string fileHash, string status)
    {
        var metadataDir = Path.Combine(_knowledgePath, topicId);
        if (!Directory.Exists(metadataDir)) return;

        foreach (var file in Directory.GetFiles(metadataDir, "元数据_*.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("file_hash", out var hash) && hash.GetString() == fileHash)
            {
                var updatedJson = json.Replace("\"status\":\"pending\"", $"\"status\":\"{status}\"");
                await File.WriteAllTextAsync(file, updatedJson);
                break;
            }
        }
    }
}
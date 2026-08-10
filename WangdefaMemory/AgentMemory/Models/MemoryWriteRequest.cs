using System.Text.Json.Serialization;

namespace Wangdefa.AgentMemory.Models;

public class MemoryWriteRequest
{
    public string TopicId { get; set; } = "";
    public string UserInput { get; set; } = "";
    public string AgentResponse { get; set; } = "";
    public PerceptionModel Perception { get; set; } = new();
    public InsightModel Insight { get; set; } = new();
    public string[]? HitRecordIds { get; set; }
    public double Confidence { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Source { get; set; } = "agent";

    [JsonIgnore]
    public Stream? FileStream { get; set; }
    public string? FileName { get; set; }
    public string? SourcePath { get; set; }
    public string? SourceType { get; set; }
    public string? FileHash { get; set; }
    public long? FileSize { get; set; }
}
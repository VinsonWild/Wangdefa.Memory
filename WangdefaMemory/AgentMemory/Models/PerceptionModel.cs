using System.Text.Json.Serialization;

namespace Wangdefa.AgentMemory.Models;

public class PerceptionModel
{
    [JsonPropertyName("文体")]
    public string Genre { get; set; } = "";

    [JsonPropertyName("时间")]
    public string Time { get; set; } = "";

    [JsonPropertyName("场景")]
    public string Scene { get; set; } = "";

    [JsonPropertyName("情绪")]
    public string Emotion { get; set; } = "";

    [JsonPropertyName("状态")]
    public string State { get; set; } = "";

    [JsonPropertyName("情景")]
    public string Context { get; set; } = "";
}
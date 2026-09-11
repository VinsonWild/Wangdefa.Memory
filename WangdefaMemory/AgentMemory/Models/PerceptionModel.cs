// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

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

    [JsonPropertyName("场景细分")]
    public string SceneSub { get; set; } = "";

    [JsonPropertyName("情绪")]
    public string Emotion { get; set; } = "";

    [JsonPropertyName("状态")]
    public string State { get; set; } = "";

    [JsonPropertyName("情景")]
    public string Context { get; set; } = "";
}
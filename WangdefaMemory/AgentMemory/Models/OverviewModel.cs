// Copyright 漏 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 概览模型 — 知识层的自然语言预览
/// 供LLM快速理解内容，决定是否需要取全文
/// 长度：100-300字
/// </summary>
public class OverviewModel
{
    public string Id { get; set; } = "";
    public string TopicId { get; set; } = "";
    public string CognitiveRecordId { get; set; } = "";
    public string Text { get; set; } = "";
    public string ContentType { get; set; } = "document";
    public int WordCount { get; set; }
    public double Confidence { get; set; } = 0.5;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
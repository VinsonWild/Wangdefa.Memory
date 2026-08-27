// Copyright 漏 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Interfaces;

/// <summary>
/// SQLite 备份工具接口 — 由主项目实现
/// </summary>
public interface ISQLiteTools
{
    Task<string> WriteRecord(
        string userInput,
        string agentResponse,
        string topicId,
        string tags,
        string summary,
        double confidence,
        string perceptionJson,
        string route,
        string overview
    );
}
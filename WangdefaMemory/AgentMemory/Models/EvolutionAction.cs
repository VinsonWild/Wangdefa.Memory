// Copyright 漏 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 标签演化操作 — 由 C 线判断，在记忆写入后执行
/// </summary>
public class EvolutionAction
{
    public string Action { get; set; } = "";
    public string Code { get; set; } = "";
    public string? TargetCode { get; set; }
    public string? Reason { get; set; }
}
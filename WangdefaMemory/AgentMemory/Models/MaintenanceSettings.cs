// Copyright 漏 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

namespace Wangdefa.AgentMemory.Models;

/// <summary>
/// 记忆体自动清理配置
/// </summary>
public class MaintenanceSettings
{
    public string CleanMode { get; set; } = "手动";
    public double CleanMinWeight { get; set; } = 0.3;
    public int CleanMinAgeDays { get; set; } = 30;
    public int CleanHour { get; set; } = 3;
    public int CleanMinute { get; set; } = 0;
    public int CleanDayOfWeek { get; set; } = 1;
    public int CleanDayOfMonth { get; set; } = 1;
}
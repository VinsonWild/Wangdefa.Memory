// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Services;

/// <summary>
/// 偏好服务：合并、去重、更新偏好
/// </summary>
public class PreferenceService
{
    /// <summary>
    /// 合并偏好列表（不覆盖，同key合并）
    /// </summary>
    public List<PreferenceEntry> Merge(List<PreferenceEntry> existing, List<PreferenceEntry> incoming)
    {
        var dict = existing.ToDictionary(p => p.Key, p => p);

        foreach (var inc in incoming)
        {
            if (dict.TryGetValue(inc.Key, out var existingPref))
            {
                // 同key：置信度累加（上限0.95）
                existingPref.Confidence = Math.Min(0.95, existingPref.Confidence + 0.05);

                // 值不同时用新值覆盖（新值更准确）
                if (existingPref.Value != inc.Value)
                {
                    existingPref.Value = inc.Value;
                    Console.WriteLine($"[PreferenceService] 偏好更新: {inc.Key} = {inc.Value}");
                }
                else
                {
                    Console.WriteLine($"[PreferenceService] 偏好强化: {inc.Key} (confidence: {existingPref.Confidence:F2})");
                }

                // scene 合并
                if (!string.IsNullOrEmpty(inc.Scene) && !existingPref.Scene.Contains(inc.Scene))
                {
                    existingPref.Scene = string.IsNullOrEmpty(existingPref.Scene)
                        ? inc.Scene
                        : existingPref.Scene + "," + inc.Scene;
                }
            }
            else
            {
                // 新增偏好
                dict[inc.Key] = inc;
                Console.WriteLine($"[PreferenceService] 新增偏好: {inc.Key} = {inc.Value}");
            }
        }

        return dict.Values.ToList();
    }
}
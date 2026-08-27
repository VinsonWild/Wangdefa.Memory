// Copyright 漏 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Thinking.Events;

namespace Wangdefa.AgentMemory.Interfaces;

/// <summary>
/// 事件存储接口
/// </summary>
public interface IEventStore
{
    Task SaveAsync(EventModel evt);
    Task<EventModel?> LoadAsync(string eventId, DateTime? date = null);
    Task<List<EventModel>> GetDayEventsAsync(DateTime date);
    Task<List<EventSummary>> GetDaySummariesAsync(DateTime date);
    Task<List<EventModel>> GetStepsAsync(string parentEventId);
    Task UpdateInsightAsync(string eventId, DialogueAnalysis insight);
    void DeleteDay(DateTime date);
}
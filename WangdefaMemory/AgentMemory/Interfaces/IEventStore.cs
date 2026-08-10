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
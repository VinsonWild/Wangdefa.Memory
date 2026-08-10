using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Interfaces;

/// <summary>
/// 思考层存储接口
/// </summary>
public interface IThinkingStore
{
    string GetTopicPath(string topicId);
    string GetChatPath(string topicId);
    Task<string> SaveIndex(DiversionIndexModel index, string topicId = "default");
    Task<DiversionIndexModel?> LoadIndex(string recordId, string? topicId = null);
    Task<string?> GetLatestRecordId(string topicId);
    Task UpdateIndex(string recordId, string topicId, DiversionIndexModel index);
    Task Update(string recordId, string topicId, object record);
    Task<ChatRecord?> LoadChatRecord(string recordId, string topicId);

    /// <summary>
    /// 从事件存储加载事件
    /// </summary>
    Task<EventModel?> LoadEvent(string eventId);
}
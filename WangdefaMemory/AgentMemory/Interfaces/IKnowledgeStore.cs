using Wangdefa.AgentMemory.Knowledge;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Thinking;

namespace Wangdefa.AgentMemory.Interfaces;

/// <summary>
/// 知识存储接口
/// </summary>
public interface IKnowledgeStore
{
    Task<string> Save(object analysis, string topicId);
    Task<string> SaveDialogueAnalysis(DialogueAnalysis analysis, string topicId);
    Task<object?> Load(string id, string topicId);
    Task<DialogueAnalysis?> LoadDialogueAnalysis(string id, string topicId);
    Task<List<KnowledgeIndexEntry>> Search(string topicId, string[] queryTags);
    Task<List<KnowledgeIndexEntry>> GetAll(string topicId);
    Task<bool> Delete(string id, string topicId);
    Task RebuildIndex(string topicId);
}
// Copyright 漏 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

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
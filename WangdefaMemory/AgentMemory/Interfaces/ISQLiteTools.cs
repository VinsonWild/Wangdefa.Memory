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
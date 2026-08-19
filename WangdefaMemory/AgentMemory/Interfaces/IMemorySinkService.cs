using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Interfaces;

/// <summary>
/// 记忆写入服务接口
/// </summary>
public interface IMemorySinkService
{
    Task SinkAsync(
        string userInput,
        string agentResponse,
        string topicId,
        PerceptionModel perception,
        string summary,
        string overview,
        List<string> tags,
        string route,
        string? sourcePath = null,
        string? sourceType = null,
        Dictionary<string, string>? missingTagDefinitions = null,
        List<PreferenceEntry>? preferences = null);

    /// <summary>
    /// 前置写入卡片框架
    /// </summary>
    /// <returns>返回卡片ID</returns>
    Task<string> WriteFrameAsync(
        string topicId,
        string userInput,
        PerceptionModel perception,
        List<string> tags,
        string route,
        string? sourcePath = null,
        string? sourceType = null);

    /// <summary>
    /// 补全卡片
    /// </summary>
    /// <param name="cardId">卡片ID</param>
    Task CompleteAsync(
        string cardId,
        string agentResponse,
        string status,
        string? errorMessage = null);
}
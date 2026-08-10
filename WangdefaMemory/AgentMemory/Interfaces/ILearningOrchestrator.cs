using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Interfaces;

/// <summary>
/// 学习机制入口接口
/// </summary>
public interface ILearningOrchestrator
{
    Task ProcessAsync(EventModel evt);
}
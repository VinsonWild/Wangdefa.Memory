using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;
using Wangdefa.AgentMemory.Thinking.Events;
using Wangdefa.Contracts;

namespace Wangdefa.AgentMemory.Thinking.KnowledgeExtractor;

/// <summary>
/// 学习机制启动规则 — 统一入口
/// 判断什么时候触发学习、调用哪个提取器
/// </summary>
public class LearningOrchestrator : ILearningOrchestrator
{
    private readonly DialogueExtractor _dialogueExtractor;
    private readonly FileExtractor _fileExtractor;
    private readonly ActionExtractor _actionExtractor;
    private readonly TaskExtractor _taskExtractor;
    private readonly IEventStore _eventStore;


    public LearningOrchestrator(IChatService chatService, IEventStore eventStore)
    {
        _dialogueExtractor = new DialogueExtractor(chatService);
        _fileExtractor = new FileExtractor(chatService);
        _actionExtractor = new ActionExtractor(chatService);
        _taskExtractor = new TaskExtractor(chatService);
        _eventStore = eventStore;
    }

    /// <summary>
    /// 处理事件：判断是否值得学习 → 调用对应提取器 → 回写事件
    /// </summary>
    public async Task ProcessAsync(EventModel evt)
    {
        // 1. 判断是否值得学习
        if (!ShouldLearn(evt))
            return;

        // 2. 调用对应的提取器
        DialogueAnalysis? insight = evt.EventType switch
        {
            "chat" => await _dialogueExtractor.ExtractAsync(evt),
            "file" => await _fileExtractor.ExtractAsync(evt),
            "action" => await _actionExtractor.ExtractAsync(evt),
            "task" => await _taskExtractor.ExtractAsync(evt),
            _ => null
        };

        if (insight == null)
            return;

        // 3. 回写事件
        await _eventStore.UpdateInsightAsync(evt.EventId, insight);
    }

    private bool ShouldLearn(EventModel evt)
    {
        // 规则：
        // - 对话：有内容就学
        // - 文件：非临时文件
        // - 行为：关键行为（打开/保存/删除）
        // - 任务：执行完成的
        // 可扩展：阈值、频率、用户反馈

        return evt.Result.Status == "completed" || evt.Result.Status == "pending";
    }
}
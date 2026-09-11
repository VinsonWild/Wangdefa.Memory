// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.

using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory.Services;

/// <summary>
/// 反馈服务：写入卡片和事件
/// </summary>
public class FeedbackService
{
    /// <summary>
    /// 写入反馈到卡片
    /// </summary>
    public void ApplyToCard(CognitiveRecordModel card, FeedbackEntry? feedback)
    {
        if (feedback != null && !string.IsNullOrEmpty(feedback.Status))
        {
            card.FeedbackStatus = feedback.Status;
            card.FeedbackReason = feedback.Reason;
            Console.WriteLine($"[FeedbackService] 卡片反馈已更新: {feedback.Status}");
        }
        // 如果本轮没有 feedback，保留卡片原有的 FeedbackStatus（不清空）
    }

    /// <summary>
    /// 写入反馈到事件
    /// </summary>
    public void SaveToEvent(EventModel? evt, FeedbackEntry? feedback)
    {
        if (evt != null && feedback != null && !string.IsNullOrEmpty(feedback.Status))
        {
            evt.Result.Extra ??= new Dictionary<string, object>();
            evt.Result.Extra["feedback"] = new
            {
                feedback.Status,
                feedback.Reason
            };
            Console.WriteLine($"[FeedbackService] 反馈已保存到事件: {feedback.Status} - {feedback.Reason}");
        }
    }
}
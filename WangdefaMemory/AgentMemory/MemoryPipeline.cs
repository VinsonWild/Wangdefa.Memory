using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.Models;

namespace Wangdefa.AgentMemory;

/// <summary>
/// 记忆体管道 - A线 + 中间件 统一入口
/// 输入用户消息，输出 enrichedInput（意图 + 记忆 + 偏好）
/// </summary>
public class MemoryPipeline
{
    private readonly IntentAnalyzer _intentAnalyzer;
    private readonly Middleware _middleware;

    public MemoryPipeline(IntentAnalyzer intentAnalyzer, Middleware middleware)
    {
        _intentAnalyzer = intentAnalyzer;
        _middleware = middleware;
    }

    /// <summary>
    /// 处理用户输入，返回 enrichedInput
    /// </summary>
    /// <param name="input">用户输入</param>
    /// <param name="sessionId">会话ID</param>
    /// <returns>MemoryPipelineResult 包含 enrichedInput 和中间结果</returns>
    public async Task<MemoryPipelineResult> ProcessAsync(string input, string sessionId = "default")
    {
        // ===== 1. A线：意图分析 =====
        var intentResult = await _intentAnalyzer.AnalyzeAsync(input, sessionId);
        Console.WriteLine($"🧠 意图分析: {intentResult.Intent}, route: {intentResult.Route}");

        // ===== 2. 中间件：特征推演 + 记忆检索 + 上下文组装 =====
        var (enrichedInput, cognitiveResult, missingTags, frameId) = await _middleware.ProcessAsync(input, sessionId, intentResult);
        Console.WriteLine($"📤 中间件完成，enrichedInput 长度: {enrichedInput.Length}");

        return new MemoryPipelineResult
        {
            EnrichedInput = enrichedInput,
            IntentResult = intentResult,
            CognitiveResult = cognitiveResult,
            MissingTags = missingTags,
            FrameId = frameId
        };
    }
}

/// <summary>
/// 记忆体管道处理结果
/// </summary>
public class MemoryPipelineResult
{
    /// <summary>
    /// 组装好的上下文字符串（意图 + 记忆 + 偏好）
    /// </summary>
    public string EnrichedInput { get; set; } = "";

    /// <summary>
    /// 意图分析结果
    /// </summary>
    public IntentAnalysisResult IntentResult { get; set; } = new();

    /// <summary>
    /// 认知匹配结果
    /// </summary>
    public CognitiveMatchResultModel? CognitiveResult { get; set; }

    /// <summary>
    /// 未命中的标签
    /// </summary>
    public StructuredTag[] MissingTags { get; set; } = Array.Empty<StructuredTag>();

    /// <summary>
    /// 框架卡片ID（由中间件写入时返回，供补全时精确定位）
    /// </summary>
    public string? FrameId { get; set; }
}
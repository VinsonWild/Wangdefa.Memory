using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using Wangdefa.AgentMemory;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.Interfaces;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;
using WangdefaMemory.MCP.Services;

namespace WangdefaMemory.MCP.Tools;

public class MemoryTools
{
    private static IWangdefaMemory? _memory;
    private static IChatService? _chatService;
    private static readonly object _lock = new();

    private static void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_memory != null) return;

            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "memory");
            var apiKey = GetApiKeyFromCredentials();

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("无法读取 DEEPSEEK_API_KEY，请检查 .credentials.yaml 文件");
            }

            _chatService = new McpChatService(apiKey);
            ServiceRegistry.Initialize(_chatService, basePath);
            _memory = ServiceRegistry.GetWangdefaMemory();
        }
    }

    private static string GetApiKeyFromCredentials()
    {
        var envKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (!string.IsNullOrEmpty(envKey) && !envKey.Contains("${"))
        {
            return envKey;
        }

        var credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dsh",
            ".credentials.yaml"
        );

        if (!File.Exists(credPath))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(credPath);
            var lines = yaml.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("DEEPSEEK_API_KEY:"))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[1].Trim().Trim('"', '\'');
                        if (!string.IsNullOrEmpty(key) && !key.Contains("${"))
                        {
                            return key;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MemoryTools] 读取凭据文件失败: {ex.Message}");
        }

        return null;
    }

    [McpServerTool]
    public static async Task<string> ProcessMessage(
        [Description("用户输入的消息")] string input,
        [Description("会话ID，用于隔离不同会话的记忆")] string? sessionId = null)
    {
        try
        {
            EnsureInitialized();

            var intentAnalyzer = new IntentAnalyzer(_chatService!);
            var middleware = new Middleware(_memory!);
            var pipeline = new MemoryPipeline(intentAnalyzer, middleware);

            var result = await pipeline.ProcessAsync(input, sessionId ?? "default");

            var response = new
            {
                enrichedInput = result.EnrichedInput,
                intent = result.IntentResult.Intent,
                hasMemory = result.CognitiveResult != null,
                memory = result.CognitiveResult != null ? new
                {
                    summary = result.CognitiveResult.Summary ?? "",
                    tags = result.CognitiveResult.ContentTags ?? Array.Empty<string>(),
                    confidence = result.CognitiveResult.Confidence,
                    preferences = result.CognitiveResult.Preferences ?? new List<PreferenceEntry>()
                } : null,
                missingTags = result.MissingTags.Length,
                frameId = result.FrameId
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            var errorResponse = new
            {
                error = $"处理消息失败: {ex.Message}",
                stackTrace = ex.StackTrace
            };
            return JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    public static async Task<string> SaveMemory(
        [Description("Agent的回复内容")] string agentResponse,
        [Description("卡片ID（由 ProcessMessage 返回的 frameId）")] string cardId,
        [Description("状态：completed / interrupted / failed")] string status = "completed",
        [Description("错误信息（当状态为 failed 时可选）")] string? errorMessage = null)
    {
        try
        {
            EnsureInitialized();

            await _memory!.CompleteMemory(
                cardId: cardId,
                agentResponse: agentResponse,
                status: status,
                errorMessage: errorMessage
            );

            var result = new
            {
                success = true,
                message = $"记忆已补全并保存，cardId: {cardId}，状态: {status}"
            };
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            var errorResult = new
            {
                success = false,
                error = $"记忆补全失败: {ex.Message}"
            };
            return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
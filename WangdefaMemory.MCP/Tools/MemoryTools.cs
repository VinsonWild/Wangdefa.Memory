// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using Wangdefa.AgentMemory;
using Wangdefa.AgentMemory.Cognitive;
using Wangdefa.AgentMemory.FeatureEngine;
using Wangdefa.AgentMemory.FeatureEngine.Models;
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

    /// <summary>
    /// 残缺标签缓存（按 cardId）
    /// ProcessMessage 写入，SaveMemory 取出后删除
    /// 用 ConcurrentDictionary 保证线程安全（MCP 可能并发调用）
    /// </summary>
    private static readonly ConcurrentDictionary<string, List<TagEntry>> _malformedCache = new();

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

            var basePath = ServiceRegistry.GetBasePath();
            var cognitiveRecordsPath = Path.Combine(basePath, "cognitive", "records");
            var intentAnalyzer = new IntentAnalyzer(_chatService!, cognitiveRecordsPath);
            var sceneStore = new SceneStore(ServiceRegistry.GetBasePath());
            var thinkingStore = ServiceRegistry.GetThinkingStore();
            var middleware = new Middleware(_memory, sceneStore, thinkingStore);
            var pipeline = new MemoryPipeline(intentAnalyzer, middleware);

            var result = await pipeline.ProcessAsync(input, sessionId ?? "default");

            // ★ 批次 E：缓存残缺标签（E-3 口径），供 SaveMemory 时传给 CompleteMemory
            if (!string.IsNullOrEmpty(result.FrameId) && result.MalformedTags.Count > 0)
            {
                _malformedCache[result.FrameId] = result.MalformedTags;
                Console.WriteLine($"[MemoryTools] 已缓存 {result.MalformedTags.Count} 个残缺标签，cardId: {result.FrameId}");
            }

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
                malformedTags = result.MalformedTags.Count,
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
        [Description("用户原始输入")] string userInput,
        [Description("Agent的回复内容")] string agentResponse,
        [Description("卡片ID（由 ProcessMessage 返回的 frameId）")] string cardId,
        [Description("状态：completed / interrupted / failed")] string status = "completed",
        [Description("错误信息（当状态为 failed 时可选）")] string? errorMessage = null)
    {
        try
        {
            EnsureInitialized();

            // ★ 批次 E：取出缓存的残缺标签（E-3 口径），用后删除
            List<TagEntry>? malformedTags = null;
            if (_malformedCache.TryRemove(cardId, out var cached))
            {
                malformedTags = cached;
                Console.WriteLine($"[MemoryTools] 取出缓存的 {malformedTags.Count} 个残缺标签，cardId: {cardId}");
            }

            await _memory!.CompleteMemory(
                cardId: cardId,
                userInput: userInput,
                agentResponse: agentResponse,
                status: status,
                errorMessage: errorMessage,
                malformedTags: malformedTags   // ★ 有就传（E-3 口径），没有就 null（走兜底）
            );

            var result = new
            {
                success = true,
                message = $"记忆已补全并保存，cardId: {cardId}，状态: {status}",
                malformedTags = malformedTags?.Count ?? 0
            };
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            // 异常时也要清缓存（避免残留）
            _malformedCache.TryRemove(cardId, out _);

            var errorResult = new
            {
                success = false,
                error = $"记忆补全失败: {ex.Message}"
            };
            return JsonSerializer.Serialize(errorResult, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
using System.Text;
using System.Text.Json;
using Wangdefa.Contracts;

namespace WangdefaMemory.MCP.Services;

public class McpChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _modelId;
    private readonly string _apiKey;

    public McpChatService(string apiKey, string modelId = "deepseek-v4-flash", string endpoint = "https://api.deepseek.com/v1")
    {
        _apiKey = apiKey;
        _modelId = modelId;
        _endpoint = endpoint.TrimEnd('/');
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<string> ChatAsync(string prompt)
    {
        var body = new
        {
            model = _modelId,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = false,
            thinking = new { type = "disabled" }
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_endpoint}/chat/completions", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[McpChatService] 请求失败: {response.StatusCode}, {responseJson}");
            return "";
        }

        using var doc = JsonDocument.Parse(responseJson);
        if (doc.RootElement.TryGetProperty("choices", out var choices) &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var contentProp))
        {
            return contentProp.GetString() ?? "";
        }

        return "";
    }

    public void SetThink(bool enabled)
    {
        // MCP 场景暂时不支持思考模式切换
    }

    public bool IsDeepSeekThinkingMode()
    {
        return false;
    }
}
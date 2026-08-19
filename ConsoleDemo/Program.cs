using Wangdefa.AgentMemory;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;

// ===== 1. 实现 IChatService（示例用模拟实现） =====
var chatService = new SampleChatService();

// ===== 2. 初始化记忆体 =====
var basePath = Path.Combine(Directory.GetCurrentDirectory(), "memory");
ServiceRegistry.Initialize(chatService, basePath);

Console.WriteLine("✅ 记忆体已初始化");
Console.WriteLine($"📁 存储路径: {basePath}");
Console.WriteLine();

// ===== 3. 写入一条记忆 =====
var memory = ServiceRegistry.GetWangdefaMemory();

Console.WriteLine("📝 写入记忆...");
await memory.SinkAsync(
    userInput: "我喜欢用简洁的风格写代码",
    agentResponse: "好的，已记录你的偏好",
    topicId: "demo",
    perception: new PerceptionModel { Scene = "工作" },
    summary: "用户偏好简洁代码风格",
    overview: "用户喜欢简洁、可读性强的代码风格",
    tags: new List<string> { "代码风格", "简洁" },
    route: "shallow"
);
Console.WriteLine("✅ 记忆已写入");
Console.WriteLine();

// ===== 4. 查询记忆 =====
Console.WriteLine("🔍 查询记忆: \"写代码时要注意什么\"");
var result = await memory.CognitiveMatch(
    input: "写代码时要注意什么",
    semanticTags: new[] { "代码风格" }
);

if (result != null)
{
    Console.WriteLine($"📖 匹配到记忆: {result.Summary}");
    Console.WriteLine($"🏷️ 标签: {string.Join(", ", result.ContentTags)}");
}
else
{
    Console.WriteLine("📭 没有找到相关记忆");
}

Console.WriteLine();
Console.WriteLine("按任意键退出...");
Console.ReadKey();

// ===== 模拟 IChatService 实现 =====
public class SampleChatService : IChatService
{
    public async Task<string> ChatAsync(string prompt)
    {
        // 模拟返回 JSON，实际使用时替换为真实的 LLM 调用
        return await Task.FromResult("{\"summary\": \"示例摘要\", \"overview\": \"示例概览\"}");
    }

    public void SetThink(bool enabled)
    {
        // 示例实现，不处理
    }

    public bool IsDeepSeekThinkingMode()
    {
        return false;
    }
}
&nbsp;
---

```markdown
# Wangdefa.Memory

**本地优先的 Agent 五层记忆体组件**

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/badge/NuGet-v1.0.0-orange.svg)](https://www.nuget.org/packages/Wangdefa.Memory/)

---

## 📖 项目简介

Wangdefa.Memory 是一个为企业级 Agent 设计的五层记忆体组件，采用「本地优先」的存储策略，达到轻量、可控、可解释。

Wangdefa.Memory 选择了无向量记忆体方向（不排除未来有弱向量辅助），将记忆分为**认知、特征推演、思考、阅历、传递**五层。

我们认为记忆来源于特征的识别与记录，这是人类与机器之间能找到的记忆共性，希望用特征记忆能力让 Agent 趋向「像人一样记住事情」的能力。

> 记忆体负责存储、检索和演化长期记忆及自我沉淀，让你的 Agent 用得越久越理解你，逐渐成为你的本地“数字分身”。

> **状态：早期阶段（Early Stage）** - 核心功能已完成，后续逐步优化推演逻辑。欢迎试用和反馈。

---

## ✨ 核心特性

| 特性 | 说明 |
|------|------|
| **五层记忆架构** | 认知层 / 特征推演 / 思考层 / 阅历层 / 传递层 |
| **特征推演引擎** | 标签池 + 密码簿 + 特征统计 + 时间衰减，让记忆通过认知驱动 |
| **偏好闭环** | 用户反馈自动转化为偏好，持续学习 |
| **意图驱动检索** | 根据意图决定记忆注入深度（shallow / medium / deep） |
| **标签演化** | 合并 / 分裂 / 弃用，标签自动优化 |
| **本地优先** | 所有数据存储在本地 SQLite + JSON |
| **轻量依赖** | 仅依赖 SQLite + System.Text.Json |
| **接口实现** | 事件记忆通过实现 `IChatService` 即可集成， 知识及资料记忆通过‘ISQLiteTools’写入实现。|

---

---

## ⚙️ 核心机制：特征推演引擎

记忆体的核心是 **特征推演引擎（FeatureEngine）**，负责记忆的匹配和排序。

### 特征推演三件套

| 组件 | 存什么 | 回答什么问题 |
|------|--------|-------------|
| **标签池（TagDictionary）** | 所有标签 + 定义 + 近义词 | "这个标签存在吗？它的 code 是什么？" |
| **密码簿（PasswordBook）** | code → 卡片ID 列表 | "这个标签关联了哪些卡片？" |
| **特征池（FeatureStats）** | 每张卡片 → 它有哪些标签 | "这张卡片有哪些标签？" |


推演流程
用户输入 → 提取标签 → 查标签池拿到 code → 查密码簿拿到卡片ID → 通过特征池确认卡片有哪些标签 → 多轮拓展推演关联→ 计算匹配强度

### 匹配流程

1. **精准匹配**：用 `tag + dimension` 查标签池，直接命中 `code`
2. **近义匹配**：用 `synonyms` 扩展匹配范围
3. **密码簿查询**：用 `code` 查密码簿，拿到卡片ID列表
4. **特征池匹配**：用卡片ID查特征池，确认卡片实际包含哪些标签，计算匹配强度
5. **时间衰减**：匹配强度 × `exp(-0.05 × 天数)`，新记忆优先
6. **排序返回**：按最终权重降序返回 TopN

### 调用方式

```csharp
// 内部自动调用特征推演，你只需要传标签
var result = await memory.CognitiveMatch(
    input: "写代码时要注意什么",
    semanticTags: new[] { "代码风格" }
);
```

## 🏗️ 架构图

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                                     记忆体架构                                     │
├─────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                     │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                         对外接口（IWangdefaMemory）                         │   │
│  │                                                                             │   │
│  │   SinkAsync()          CognitiveMatch()          AddTagWithSynonyms()       │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                    │                                               │
│                                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                         核心：特征推演引擎（FeatureEngine）                   │   │
│  │                                                                             │   │
│  │   ┌───────────────┐    ┌───────────────┐    ┌───────────────┐              │   │
│  │   │   标签池       │    │   密码簿       │    │   特征统计     │              │   │
│  │   │ TagDictionary │    │ PasswordBook  │    │ FeatureStats  │              │   │
│  │   │               │    │               │    │               │              │   │
│  │   │ tag → code    │    │ code → 卡片ID │    │ 命中次数      │              │   │
│  │   │ synonyms      │    │               │    │ 最后命中时间   │              │   │
│  │   │ definition    │    │               │    │               │              │   │
│  │   └───────────────┘    └───────────────┘    └───────────────┘              │   │
│  │                                                                             │   │
│  │   匹配流程：                                                                 │   │
│  │   标签输入 → 精准匹配 → 近义匹配 → 时间衰减排序 → 返回卡片ID                │   │
│  │                                                                             │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                    │                                               │
│                                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                         认知层（CognitiveReader）                            │   │
│  │                                                                             │   │
│  │   特征推演返回的卡片ID → 加载认知卡片 → 返回 CognitiveMatchResult           │   │
│  │                                                                             │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                    │                                               │
│                                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                         存储层（L2 + L3）                                    │   │
│  │                                                                             │   │
│  │   ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐        │   │
│  │   │  思考层          │    │  阅历层          │    │  知识层          │        │   │
│  │   │ ThinkingStore   │    │  EventStore     │    │ KnowledgeStore  │        │   │
│  │   │                 │    │  MemorySink     │    │                 │        │   │
│  │   │ 分流索引         │    │  事件存储        │    │ 概览+摘要        │        │   │
│  │   └─────────────────┘    └─────────────────┘    └─────────────────┘        │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                     │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 🧩 各层职责

| 层级 | 名称 | 核心组件 | 职责 |
|------|------|----------|------|
| **L1** | 认知层 | `CognitiveReader` | 负责语义提取后快速读取认知卡片，通过特征推演检索记忆 |
| **L2** | 思考层 | `ThinkingStore` | 负责考虑内容深度和学习存储，进行分流索引，并记录「去哪找」 |
| **L3** | 阅历层 | `EventStore`、`KnowledgeStore`、`MemorySinkService` | 存储每一次交互的事件、知识的完整内容、概览和概要，并进行认知卡片的写入 |
| **L4** | 特征推演 | `FeatureEngine`（标签池 + 密码簿 + 特征统计） | 标签匹配、近义扩展、时间衰减排序 |
| **L5** | 传递层 | 内置于 `Middleware` | 根据 `route` 决定记忆注入深度（shallow / medium / deep） |

---

## 📂 存储目录结构

```
memory/
├── chat_history.db                         ← 聊天历史
├── wangdefa_memory.db                      ← SQLite 备份
├── cognitive/
│   └── records/
│       └── 认知_xxx.json                   ← L1 认知层
├── experience/
│   ├── events/
│   │   └── 2026-08-10/
│   │       └── 事件_xxx.json              ← L3 阅历层（事件）
│   └── knowledge/
│       └── {topicId}/
│           ├── 概览_xxx.json              ← L3 阅历层（知识）
│           └── 摘要_xxx.json              ← L3 阅历层（知识）
└── thinking/
    └── chat/
        └── {topicId}/
            └── 记录_xxx.json              ← L2 思考层（分流索引）
```

---

## 🔁 数据流

```
用户输入
    ↓
L1 认知层（CognitiveReader）→ 获取语义后读取认知卡片
    ↓
L4 特征推演（FeatureEngine）→ 标签匹配 + 时间衰减排序
    ↓
L2 思考层（ThinkingStore）→ 分流索引（记录去哪找）
    ↓
L5 传递层（Middleware）→ 路由分流（浅/中/深）
    ↓
L3 阅历层（EventStore / KnowledgeStore / MemorySinkService）→ 存储事件 + 知识
```

---

## 写入逻辑

```
传入参数：
userInput, agentResponse, perception, summary, overview, tags, route
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  MemorySinkService.SinkAsync()                                  │
│                                                                 │
│  ① 构建认知记录                                                 │
│     recordId = 认知_20260810_143022                            │
│     Insight = { ContentTags, Summary, Preferences }            │
│     ↓                                                          │
│  ② 写入 JSON 文件                                              │
│     memory/cognitive/records/认知_20260810_143022.json        │
│     ↓                                                          │
│  ③ TagCard（特征池更新）                                       │
│     FeatureEngine.TagCard(recordId, tags)                     │
│     ├── 标签池：新标签 + definition                            │
│     ├── 密码簿：code → 卡片ID                                  │
│     └── 特征统计：命中次数 +1                                  │
│     ↓                                                          │
│  ④ 分流索引（ThinkingStore.SaveIndex）                         │
│     memory/thinking/chat/{topicId}/记录_xxx.json              │
│     存：SummaryPointer / OverviewPointer / FullTextPointer    │
│     ↓                                                          │
│  ⑤ 知识层（概览 + 摘要）                                       │
│     memory/experience/knowledge/{topicId}/概览_xxx.json      │
│     memory/experience/knowledge/{topicId}/摘要_xxx.json      │
│     ↓                                                          │
│  ⑥ SQLite 备份                                                │
│     memory/wangdefa_memory.db                                 │
│     ↓                                                          │
│  ⑦ 事件存储                                                   │
│     memory/experience/events/{date}/事件_xxx.json             │
│     ↓                                                          │
│  ⑧ 后台学习（异步）                                           │
│     LearningOrchestrator.ProcessAsync(evt)                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 查询逻辑

```
传入参数：
input, semanticTags, topicId
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  CognitiveReader.Match()                                       │
│                                                                 │
│  ① 语义标签 → 标签 code                                        │
│     semanticTags: ["规划", "开源"]                             │
│     FeatureEngine.Tags.GetCode(tag) → code                    │
│     ↓                                                          │
│  ② 特征推演检索                                               │
│     FeatureEngine.Search(codes)                               │
│     ├── 精准匹配：tag + dimension → code                      │
│     ├── 近义匹配：synonyms → code                             │
│     ├── 时间衰减：匹配强度 × exp(-0.05 × 天数)               │
│     └── 排序返回 TopN                                         │
│     ↓                                                          │
│  ③ 加载认知卡片                                               │
│     LoadCognitiveRecord(cardId)                               │
│     memory/cognitive/records/认知_xxx.json                    │
│     ↓                                                          │
│  ④ 加载关联数据                                               │
│     Perception（从事件加载）                                   │
│     DiversionIndex（从思考层加载）                             │
│     ↓                                                          │
│  ⑤ 返回 CognitiveMatchResult                                  │
│     { Summary, ContentTags, Preferences, Confidence,          │
│       SourcePath, RecordId }                                  │
└─────────────────────────────────────────────────────────────────┘
```



---

## 🔌 上游系统需要做什么

记忆体不包含 LLM 调用，你需要在上游系统（或 Agent）中完成语义提取：

1. **用户输入 → 调用 LLM**
2. **LLM 提取结构化标签**：从用户输入中提取 `tag`、`dimension`、`definition`、`synonyms`
3. **将标签传给记忆体**：调用 `SinkAsync()` 写入，或 `CognitiveMatch()` 查询
4. **记忆体只负责推演、查询、写入，具体接入用法看个人。

```csharp
// 上游系统示例
var userInput = "帮我规划开源项目";

// 1. 调用 LLM 提取标签（你自己实现）
var tags = await YourLLM.ExtractTags(userInput);
// tags = [{ tag: "规划", dimension: "任务", synonyms: ["计划", "筹备"] }]

// 2. 传给记忆体
await memory.SinkAsync(
    userInput: userInput,
    agentResponse: agentResponse,
    topicId: topicId,
    perception: perception,
    summary: summary,
    overview: overview,
    tags: tags,
    route: "shallow"
);
```

---

## 🚀 快速开始

### 1. 实现 IChatService

记忆体需要调用模型来做摘要分析和学习，你需要实现这个接口：

```csharp
using Wangdefa.Contracts;

public class MyChatService : IChatService
{
    public async Task<string> ChatAsync(string prompt)
    {
        // 调用你的模型（OpenAI / Ollama / DeepSeek 等）
        return await YourModel.CallAsync(prompt);
    }

    public void SetThink(bool enabled)
    {
        // 可选：设置思考模式
    }

    public bool IsDeepSeekThinkingMode()
    {
        return false;  // 根据你的模型返回
    }
}
```

### 2. 初始化记忆体

```csharp
using Wangdefa.AgentMemory;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;

var chatService = new MyChatService();
var basePath = Path.Combine(Directory.GetCurrentDirectory(), "memory");

ServiceRegistry.Initialize(chatService, basePath);
var memory = ServiceRegistry.GetWangdefaMemory();
```

### 3. 写入记忆

```csharp
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
```

### 4. 查询记忆

```csharp
var result = await memory.CognitiveMatch(
    input: "写代码时要注意什么",
    semanticTags: new[] { "代码风格" }
);

if (result != null)
{
    Console.WriteLine($"匹配到记忆: {result.Summary}");
    // 输出：用户偏好简洁代码风格
}
```

---

## 📦 安装

### 从源码构建

```bash
git clone https://github.com/yourname/Wangdefa.Memory.git
cd Wangdefa.Memory
dotnet build
```

### NuGet（即将支持）

```bash
dotnet add package Wangdefa.Memory
```

---

## 📝 接口说明

### IWangdefaMemory

| 方法 | 说明 |
|------|------|
| `CognitiveMatch()` | 根据语义标签匹配记忆 |
| `CognitiveMatchByCodes()` | 根据标签 code 匹配记忆 |
| `CognitiveMatchTopN()` | 匹配多条记忆，返回 TopN |
| `SinkAsync()` | 写入记忆（含认知卡片、事件、知识） |
| `AddTag()` | 添加标签 |
| `AddTagWithSynonyms()` | 添加标签（含近义词） |
| `GetTagCode()` | 获取标签 code |
| `GetTagEntryByCode()` | 获取标签条目 |
| `ExecuteEvolutionAsync()` | 执行标签演化（合并 / 分裂 / 弃用） |
| `CleanMemoryAsync()` | 清理低权重记忆 |
| `GetOverview()` | 获取概览 |
| `GetFullText()` | 获取原文 |
| `DeepSearch()` | 深度检索 |

---

## 🤝 贡献

欢迎贡献！请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 了解详情。

1. Fork 本仓库
2. 创建你的分支 (`git checkout -b feature/amazing-feature`)
3. 提交你的修改 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 提交 Pull Request

### 要求
- 所有测试必须通过 (`dotnet test`)
- 新功能需要包含测试
- 保持代码风格与现有代码一致

---

## 📄 License

Apache License 2.0 © 2026 Wangdefa Memory Contributors

See [LICENSE](LICENSE) for details.
```

---


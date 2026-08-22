
```markdown
# Wangdefa.Memory

**本地优先的 Agent 五层记忆体组件**

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/badge/NuGet-v1.1.5-orange.svg)](https://www.nuget.org/packages/Wangdefa.Memory/)
[![DSH Plugin](https://img.shields.io/badge/DSH-Plugin-blue.svg)](https://github.com/topics/dsh-plugin)

---

## 📖 项目简介

Wangdefa.Memory 是一个为个人助理Agent 设计的五层记忆体组件，数据完全保留在本地，不依赖云端，达到轻量、白盒可控、可解释，未来将进一步往企业级原生记忆体方向拓展。

Wangdefa.Memory 选择了无向量记忆体方向（不排除未来有弱向量辅助），将记忆分为**认知、特征推演、思考、阅历、传递**五层。

我们认为记忆来源于对事件特征的识别与记录，特征记忆是人类与机器之间能找到的记忆共性，而机器的优势在于能记住大量特征标签，所以这个项目希望以特征记忆能力为主要核心，让 Agent 趋向「像人一样记住事情」的能力。

> 记忆体负责存储、检索和演化长期记忆及自我沉淀，并实现自我清理迭代，通过长期累计配合，让你的 Agent 用得越久越理解你，逐渐成为你的本地“数字分身”。

> **状态：早期阶段（Early Stage）** - 核心功能已完成，后续逐步优化推演逻辑。欢迎试用和反馈。

---

## ✨ 核心特性

| 特性 | 说明 |
|------|------|
| **五层记忆架构** | 认知层 / 特征推演 / 思考层 / 阅历层 / 传递层 |
| **特征推演引擎** | 标签池 + 密码簿 + 特征统计 + 时间衰减，让记忆通过认知驱动 |
| **两阶段写入** | 先写框架（pending），后补全（completed），支持状态标记 |
| **自我迭代** | 权重衰减 + 定期清理 + 标签演化，高频记忆自然沉淀，低频记忆自动遗忘 |
| **偏好闭环** | 用户反馈自动转化为偏好，持续学习 |
| **意图驱动检索** | 根据意图决定记忆注入深度（shallow / medium / deep） |
| **标签演化** | 合并 / 分裂 / 弃用，标签自动优化 |
| **本地优先** | 所有数据存储在本地 SQLite + JSON |
| **轻量依赖** | 仅依赖 SQLite + System.Text.Json |
| **MCP 适配** | 支持通过 MCP 协议接入 DSH，提供 ProcessMessage / SaveMemory 工具 |
| **A线近期记忆参考** | 意图分析时自动注入最近10张认知卡摘要和标签，提升标签提取准确性 |

---

## 📦 NuGet 安装

```bash
dotnet add package Wangdefa.Memory
```

---

## 🔌 DSH 插件使用

如果你使用的是 DeepSeek Harness（DSH），可以直接将 Wangdefa.Memory 作为 MCP 插件接入。

### 前置条件

- 已安装 [.NET 10.0](https://dotnet.microsoft.com/download) 或更高版本
- 已配置 `DEEPSEEK_API_KEY` 环境变量（DeepSeek API Key）
- （可选）如需自定义安装路径，可设置 `WANGDEFA_MEMORY_PATH` 环境变量

### 安装

**方式一：从 GitHub 安装（推荐）**

```bash
dsh plugin add github:VinsonWild/WangdefaMemory

```

**方式二：本地安装**

```bash
git clone https://github.com/你的用户名/WangdefaMemory.git
cd WangdefaMemory
dotnet build -c Release
dsh plugin add ./WangdefaMemory.MCP
```

### 配置

在 DSH 的 `cordis.patch.yml` 中配置 API Key：

```yaml
- insert:
    - id: mcp-wangdefaMemory
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: WangdefaMemory
        transport: stdio
        command: "dotnet"
        args:
          - "exec"
          - "<你的路径>/WangdefaMemory.MCP/bin/Release/net10.0/WangdefaMemory.MCP.dll"
        cwd: "<你的路径>/WangdefaMemory.MCP"
        env:
          DEEPSEEK_API_KEY: '${DEEPSEEK_API_KEY}'
```

### 使用

在 DSH 对话中调用 MCP 工具：

**1. 处理用户消息（写框架）**

```
mcp__WangdefaMemory__process_message 帮我记录一下：我喜欢用简洁的代码风格
```

返回示例：
```json
{
  "enrichedInput": "...",
  "intent": "闲聊",
  "hasMemory": false,
  "frameId": "认知_20260819_143022"
}
```

**2. 补全记忆（填内容）**

拿到 `frameId` 后，调用 `save_memory` 补全：

```
mcp__WangdefaMemory__save_memory 好的，已记录你的偏好 认知_20260819_143022 completed
```

返回示例：
```json
{
  "success": true,
  "message": "记忆已补全并保存，cardId: 认知_20260819_143022，状态: completed"
}
```

**3. 查询记忆**

下次对话时，记忆体会自动检索相关记忆：

```
mcp__WangdefaMemory__process_message 写代码时要注意什么
```

如果命中，返回的 `hasMemory` 为 `true`，`memory` 字段包含摘要和标签。

### 状态说明

| 状态 | 含义 |
|------|------|
| `pending` | 框架已建，内容待补全 |
| `completed` | 已补全，可被检索 |
| `interrupted` | 补全中断 |
| `failed` | 补全失败 |

---

## 🚀 快速开始（.NET 开发者）

### 1. 初始化记忆体

```csharp
using Wangdefa.AgentMemory;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;

// 如果不需要内置 A线，可传入 null 或 Mock 实现
var chatService = new MyChatService();
var basePath = Path.Combine(Directory.GetCurrentDirectory(), "memory");

ServiceRegistry.Initialize(chatService, basePath);
var memory = ServiceRegistry.GetWangdefaMemory();
```

### 2. 写入记忆（两阶段）

```csharp
// 阶段一：写框架（自动提取标签）
var frameId = await memory.WriteMemoryFrame(
    topicId: "demo",
    userInput: "我喜欢用简洁的风格写代码",
    perception: new PerceptionModel { Scene = "工作" },
    tags: new List<string> { "代码风格", "简洁" },  // 可传空，由 A线 自动提取
    route: "shallow"
);

// 阶段二：补全
await memory.CompleteMemory(
    cardId: frameId,
    agentResponse: "好的，已记录你的偏好",
    status: "completed"
);
```

### 3. 查询记忆

```csharp
// 不传 semanticTags 时，记忆体自动调用 A线 提取标签
var result = await memory.CognitiveMatch(
    input: "写代码时要注意什么",
    semanticTags: null  // 自动提取
);

if (result != null)
{
    Console.WriteLine($"匹配到记忆: {result.Summary}");
}
```

---

## ⚙️ 核心机制：特征推演引擎

记忆体的核心是 **特征推演引擎（FeatureEngine）**，负责记忆的匹配和排序。

### 特征推演三件套

| 组件 | 存什么 | 回答什么问题 |
|------|--------|-------------|
| **标签池（TagDictionary）** | 所有标签 + 定义 + 近义词 | "这个标签存在吗？它的 code 是什么？" |
| **密码簿（PasswordBook）** | code → 卡片ID 列表 | "这个标签关联了哪些卡片？" |
| **特征统计（FeatureStats）** | 每张卡片 → 它有哪些标签 | "这张卡片有哪些标签？" |

推演流程：
用户输入 → 提取标签 → 查标签池拿到 code → 查密码簿拿到卡片ID → 通过特征池确认卡片有哪些标签 → 多轮拓展推演关联 → 计算匹配强度

### 匹配流程

1. **精准匹配**：用 `tag + dimension` 查标签池，直接命中 `code`
2. **近义匹配**：用 `synonyms` 扩展匹配范围（作为兜底）
3. **密码簿查询**：用 `code` 查密码簿，拿到卡片ID列表
4. **特征池匹配**：用卡片ID查特征池，确认卡片实际包含哪些标签，计算匹配强度
5. **时间衰减**：匹配强度 × `exp(-0.05 × 天数)`，新记忆优先
6. **状态过滤**：只返回 `completed` 状态的卡片，过滤 `pending` 空卡
7. **排序返回**：按最终权重降序返回 TopN

---

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
│  │   WriteMemoryFrame()   CompleteMemory()                                     │   │
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
│  │   标签输入 → 精准匹配 → 近义匹配 → 时间衰减排序 → 状态过滤 → 返回卡片ID     │   │
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
│  │   │ 分流索引         │    │  事件存储        │    │  概览+摘要        │        │   │
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
├── feature_pool.db                         ← 标签池 + 密码簿 + 特征统计
├── cognitive/
│   └── records/
│       └── 认知_xxx.json                   ← L1 认知层（含 Status 状态标记）
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

### 写入流程（两阶段）

```
阶段一：写框架（WriteMemoryFrame）
用户输入 → A线 提取标签 → 中间件 → 写框架（Status = pending）
    ├── 创建认知卡片（标签 + 感知信息）
    ├── 写入密码簿（code → 卡片ID）
    └── 返回 frameId

阶段二：补全（CompleteMemory）
Agent 生成回复 → 调用 SaveMemory(frameId, agentResponse)
    ├── 填充 Summary
    ├── 更新 Status → completed / interrupted / failed
    ├── 更新特征统计（提高检索权重）
    └── 记忆可被检索
```

### 查询流程

```
用户输入 → A线 提取标签（参考最近 10 张认知卡）→ 中间件
    ├── 特征推演检索（标签匹配 + 时间衰减）
    ├── 状态过滤（只返回 completed 卡片）
    └── 返回 CognitiveMatchResult
```

---

## 📝 接口说明

### IWangdefaMemory

| 方法 | 说明 |
|------|------|
| `CognitiveMatch()` | 根据语义标签匹配记忆（`semanticTags` 可空，空则自动提取） |
| `CognitiveMatchByCodes()` | 根据标签 code 匹配记忆 |
| `CognitiveMatchTopN()` | 匹配多条记忆，返回 TopN |
| `WriteMemoryFrame()` | 写框架（状态 pending），返回 frameId |
| `CompleteMemory()` | 补全卡片，更新状态和内容 |
| `SinkAsync()` | 一次性写入（兼容旧模式） |
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

## 📄 更新说明


### v1.1.5 (2026-08-22)

重构 DSH 插件为独立自包含子目录，支持用户一条命令部署。

插件版本对齐引擎版本，统一为 1.1.5。

插件依赖改为可选 peer 依赖，由 DSH 环境运行时提供，避免私有包安装失败。

编译产物 lib/ 已提交至 git，用户无需本地构建即可加载插件。

根目录新增 dsh.bundle 声明和 cordis.patch.yml，作为 dsh plugin add 的入口路由。

删除旧的根目录 index.js 和 postinstall.js，入口统一由子目录接管。

引擎下载逻辑由插件的 ensureEngine() 在首次运行时自动完成。

优化 CI 发布脚本，仅保留引擎 zip 打包、上传 Release 和 NuGet 推送。

修复根目录 package.json description 乱码。

修复 tsconfig.json 自包含配置，moduleResolution 改为 bundler。


### v1.1.4 (2026-08-22)
1. 修复记忆写入后无法检索的问题（核心修复）
修复了 PasswordBook.Add 方法中 SQL INSERT 语句缺少 card_type 列，导致标签-卡片关联写入静默失败的问题。

影响：记忆能正常写入但后续检索不到历史卡片。

修复：

PasswordBook.Add 补上 card_type 字段，默认值 "cognitive"

表定义添加 DEFAULT 'cognitive' 作为防御

2. MCP Server stdout 日志污染协议
修复了 MCP Server 中 Console.WriteLine 日志输出到 stdout 导致 JSON-RPC 通信断裂的问题。

修复：Program.cs 添加 Console.SetOut(Console.Error);，将所有日志重定向到 stderr，避免污染协议通道。


### v1.1.3 (2026-08-21)

🐛 Bug 修复
修复 MCP 版 C线缺失问题：CompleteAsync 接回 SummaryAnalyzer，补全时调用 LLM 生成摘要、概览、缺失标签定义和偏好

修复 structuredTags 传递断裂问题：改为从卡片反查标签，移除外部传入依赖

修复概览落盘缺失问题：CompleteAsync 补全时同步写入 knowledge/{topicId}/概览_xxx.json

修复卡片 SourcePath 指针未保存问题：改完后重新保存卡片，确保 medium/deep 路由能正确读取

修复思考层索引与卡片关联断裂问题：统一使用相同时间戳后缀，确保认知卡 → 索引 → 事件的指针链路完整

修复 pending 状态未正确标记问题：卡片框架写入时状态标记为 pending，补全后更新为 completed

🔧 接口变更
CompleteMemory/CompleteAsync 增加 userInput 参数

SaveMemory 移除 structuredTagsJson 参数

CognitiveRecordModel 增加 EventId 字段

📦 依赖调整
MemorySinkService 接入 IChatService，ServiceRegistry 同步适配

✅ 测试
MemorySinkServiceTests 增加 Mock<IChatService>，确保单测通过

一句话总结：MCP 版 C线补全能力已恢复，指针链路已理顺，接口已统一。

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


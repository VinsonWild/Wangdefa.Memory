# Changelog / 更新记录


## v1.1.8 (2026-09-11)

### English
- **Preference and feedback separation:** Preferences and feedback are now extracted and stored independently.
- **Feedback-aware retrieval:** Memories the user confirmed rank higher, rejected ones are discarded, and neutral ones stay neutral.
- **Scene-filtered preferences:** Only preferences matching the current scene are injected, so preferences from different scenes don't interfere with each other.
- **Response style constraints:** A-Line judges whether the reply should be concise, balanced, detailed, or executive, and injects the corresponding length constraint.
- **Previous-turn context optimization:** Feedback judgment reads the full overview of the previous turn for more complete context.
- **Unified storage with atomic writes:** All writes go through a single entry point, writing to a temp file first and then replacing, so power loss won't corrupt data.
- **Idempotent tag pool writes:** Duplicate writes to the same tag automatically reuse the existing entry, no more database conflicts.
- **Tag pool responsibility split:** The tag pool is split into independent modules — cache, storage, evolution, and similarity each handle their own job.
- **A-Line read-only:** A-Line only reads, never writes. New tags are left to C-Line, so the tag pool stays clean.
- **Independent scene store:** Scene categories and subcategories are stored independently and accumulate over time.
- **Two-level scene support:** Supports two-level scenes like "Work / Code Review", usable in both cards and retrieval.
- **C-Line finalization with A-Line fallback:** C-Line leads, A-Line backs up. Values are resolved per field, so scene information is never lost.
- **Scene weight in retrieval:** Memories from the same scene rank higher, while cross-scene but content-relevant memories are not dropped.
- **Event retrieval fix:** Deep mode now correctly reads the full event.
- **Medium read fix:** Medium mode now uses the correct overview pointer.
- **Tag quality constraints:** The prompt forbids generic words, requires tags to be locatable, and limits the count to 2–4.
- **A-Line tag task shift:** A-Line moved from "extracting keywords" to "inferring possibly related historical memories", outputting retrieval clues.
- **Generic tag interception:** Generic words no longer enter the tag pool and pollute retrieval.
- **Tag merge redirection:** When a merged old tag is matched, it automatically redirects to the merged new tag.
- **Tag merge semantic inheritance:** When merging, the old tag's synonyms, definitions, relations, and statistics are carried over to the new tag, so no semantics are lost.

### 中文
- **偏好与反馈分离：** 偏好和反馈独立提取、独立存储，各管各的。
- **反馈感知检索：** 用户认可过的记忆优先出现，否定过的直接丢弃，没表态的保持中性。
- **按场景过滤偏好：** 只注入匹配当前场景的偏好，不同场景的偏好不会互相干扰。
- **回复风格约束：** A线判断本轮该用简洁、适中、详细还是决策风格，并注入对应的字数约束。
- **上一轮上下文优化：** 反馈判断读取上一轮的概览原文，判断依据更完整。
- **统一存储与原子写入：** 所有写入收敛到统一入口，先写临时文件再替换，断电也不会损坏。
- **标签池写入幂等：** 重复写入同一标签会自动复用，不再触发数据库冲突。
- **标签池职责拆分：** 标签池拆成独立模块，缓存、存储、演化、相似度计算各管各的。
- **A线只读不写：** A线只读不写，新标签留给C线统一处理，不再污染标签池。
- **场景库独立建表：** 场景大类+细分独立存储，可持续积累。
- **场景细分支持：** 支持“工作/代码评审”这样的两级场景，卡片和检索都能用上。
- **C线场景定稿 + A线兜底：** C线为主、A线兜底，按字段分别取值，场景信息不会丢。
- **检索场景权重：** 同场景的记忆优先出现，跨场景但内容相关的记忆不丢。
- **事件读取链路修复：** deep 模式能正确读到完整事件。
- **medium 读取修复：** medium 模式改用正确的概览指针。
- **标签质量约束：** Prompt 禁止泛用词，要求标签可定位，数量收紧到 2-4 个。
- **A线标签任务转型：** A线从“提取关键词”改为“推测可能关联的历史记忆”，输出检索线索。
- **泛用标签拦截：** 泛用词不会进入标签池污染检索。
- **标签合并重定向：** 匹配到已合并的老标签后，自动重定向到合并后的新标签。
- **标签合并语义继承：** 合并时把老标签的近义词、释义、关联、统计一起搬到新标签，语义不丢。


##  v1.1.7 (2026-09-01)

### English
- **Preferences and feedback system refactored:** C-Line now extracts preferences and feedback as independent fields. Preferences are stored with scene tags and merged incrementally (not overwritten). Feedback is stored separately to events and cards, with confirmed/rejected/partial/ignored status.
- **Feedback-aware retrieval:** CognitiveReader now filters and adjusts weights based on feedback status: confirmed gets ×1.15 boost, rejected cards are discarded, ignored and partial remain neutral.
- **Scene-based preference filtering:** Middleware now injects only preferences matching the current scene (or preferences without scene tags as universal).
- **Response style constraints:** A-Line now outputs response_style (concise/balanced/detailed/executive), and Middleware injects word count constraints accordingly.
- **Previous turn context:** previousAgentResponse now uses the full overview instead of summary for better feedback judgment.
- **Fixed scene field not being parsed in preferences:** PreferenceEntry.Scene now correctly populated from LLM output.

### 中文
- **偏好与反馈系统重构：** C线现在将偏好和反馈作为独立字段提取。偏好携带场景标签并以合并方式存储（不覆盖），反馈独立存储到事件和卡片，状态分为 confirmed/rejected/partial/ignored。
- **反馈感知检索：** CognitiveReader 根据反馈状态过滤和调整权重：confirmed 权重 ×1.15，rejected 卡片直接丢弃，ignored 和 partial 保持中性。
- **按场景过滤偏好：** Middleware 现在只注入匹配当前场景的偏好（无场景标签的偏好作为通用偏好）。
- **回复字数约束：** A线新增 response_style 输出（concise/balanced/detailed/executive），Middleware 据此注入字数约束。
- **上一轮上下文优化：** previousAgentResponse 改为读取概览原文，为反馈判断提供更完整的上下文。
- **修复偏好场景字段未被解析的问题：** PreferenceEntry.Scene 现在能正确从 LLM 输出中填充。

## v1.1.6 (2026-08-28)

### English
- **C-Line automatic tag merging:** New tags are marked as `unexamined`. During C-Line completion, the system automatically checks for tags with the same name, uses Jaccard coefficient for coarse filtering + LLM for fine judgment, and merges semantically identical tags to reduce tag pool redundancy and improve retrieval accuracy. No additional LLM calls, no user blocking.
- **Fixed synonym tag creation issue:** `AddWithSynonyms` no longer recursively creates independent tags for each synonym. Synonyms are now stored only in the main tag's `synonyms` field, preventing a large number of `unexamined` tags from permanently lingering.
- **Fixed ContentTags sync issue after tag merge:** When merging tags, all associated cards' `ContentTags` are now updated synchronously, replacing old tag names with the target tag name, keeping card display consistent with tag pool state.
- **Tag status naming optimization:** Changed from `pending` to `unexamined` to avoid confusion with card `pending` status.
- **Clean up newTagNames dead chain:** Deprecated the explicit delivery chain from Middleware → MemoryPipeline, unified to use ContentTags reverse lookup for cleaner code.

### 中文
- **C线新增标签自动合并机制：** 新建标签标记为 `unexamined`（待审），C线补全时自动检查同名标签，通过交并比粗筛 + LLM 精判，将语义相同的标签自动合并，减少标签池冗余，提升检索精度。不新增 LLM 调用，不阻塞用户。
- **修复近义词被错误创建为独立标签的问题：** `AddWithSynonyms` 不再递归为每个近义词创建独立标签，近义词仅作为主标签的 `synonyms` 字段值存储，避免大量 `unexamined` 标签永久滞留待审状态。
- **修复标签合并后卡片 ContentTags 不同步的问题：** 合并标签时同步更新所有关联卡片的 `ContentTags`，将旧标签名替换为目标标签名，保持卡片展示与标签池状态一致。
- **标签待审状态命名优化：** 从 `pending` 改为 `unexamined`，避免与卡片 `pending` 状态混淆。
- **清理 newTagNames 死链：** 废弃 Middleware → MemoryPipeline 的显式传递链路，统一走 ContentTags 反查，代码更清晰。


## v1.1.5 (2026-08-22)

### English
- Refactored DSH plugin as a standalone self-contained subdirectory, supporting one-command deployment.
- Plugin dependencies changed to optional peer dependencies, provided by DSH runtime at runtime, avoiding private package installation failures.
- Compiled `lib/` artifacts committed to git — users can load the plugin without local builds.
- Added `dsh.bundle` declaration and `cordis.patch.yml` as entry routing for `dsh plugin add`.
- Removed old root `index.js` and `postinstall.js`, unified entry under subdirectory.
- Engine download logic now handled by plugin's `ensureEngine()` on first run.
- Optimized CI release script: only engine zip packaging, Release upload, and NuGet push remain.

### 中文
- 重构 DSH 插件为独立自包含子目录，支持用户一条命令部署。
- 插件依赖改为可选 peer 依赖，由 DSH 环境运行时提供，避免私有包安装失败。
- 编译产物 `lib/` 已提交至 git，用户无需本地构建即可加载插件。
- 根目录新增 `dsh.bundle` 声明和 `cordis.patch.yml`，作为 `dsh plugin add` 的入口路由。
- 删除旧的根目录 `index.js` 和 `postinstall.js`，入口统一由子目录接管。
- 引擎下载逻辑由插件的 `ensureEngine()` 在首次运行时自动完成。
- 优化 CI 发布脚本，仅保留引擎 zip 打包、上传 Release 和 NuGet 推送。


## v1.1.4 (2026-08-22)

### English
- **Fixed issue where memory could not be retrieved after writing (critical fix):** Fixed missing `card_type` column in SQL INSERT statement in `PasswordBook.Add`, which caused tag-card association writes to fail silently. Impact: Memory could be written normally but historical cards could not be retrieved later. Fix: Added `card_type` field to `PasswordBook.Add`, default value `"cognitive"`; added `DEFAULT 'cognitive'` to table definition as defense.
- **MCP Server stdout log pollution:** Fixed issue where `Console.WriteLine` logs in MCP Server were output to stdout causing JSON-RPC communication breaks. Fix: Added `Console.SetOut(Console.Error);` in `Program.cs`, redirecting all logs to stderr to avoid protocol channel pollution.

### 中文
- **修复记忆写入后无法检索的问题（核心修复）：** 修复了 `PasswordBook.Add` 方法中 SQL INSERT 语句缺少 `card_type` 列，导致标签-卡片关联写入静默失败的问题。影响：记忆能正常写入但后续检索不到历史卡片。修复：`PasswordBook.Add` 补上 `card_type` 字段，默认值 `"cognitive"`；表定义添加 `DEFAULT 'cognitive'` 作为防御。
- **MCP Server stdout 日志污染协议：** 修复了 MCP Server 中 `Console.WriteLine` 日志输出到 stdout 导致 JSON-RPC 通信断裂的问题。修复：`Program.cs` 添加 `Console.SetOut(Console.Error);`，将所有日志重定向到 stderr，避免污染协议通道。


## v1.1.3 (2026-08-21)

### English
- **Fixed missing C-Line in MCP version:** `CompleteAsync` reconnected to `SummaryAnalyzer`, calling LLM to generate summary, overview, missing tag definitions, and preferences during completion.
- **Fixed `structuredTags` passing break:** Changed to reverse lookup from card, removed external dependency.
- **Fixed overview not being persisted:** `CompleteAsync` now writes to `knowledge/{topicId}/overview_xxx.json`.
- **Fixed card `SourcePath` pointer not saved:** Re-save card after completion to ensure medium/deep routing works correctly.
- **Fixed thinking layer index and card association break:** Unified timestamp suffix ensures cognitive card → index → event pointer chain is complete.
- **Fixed `pending` status not correctly marked:** Card frame writes with `pending` status, updated to `completed` after completion.
- **API Changes:** `CompleteMemory` / `CompleteAsync` added `userInput` parameter; `SaveMemory` removed `structuredTagsJson` parameter; `CognitiveRecordModel` added `EventId` field.
- **Dependency Adjustments:** `MemorySinkService` now uses `IChatService`, `ServiceRegistry` adapted accordingly.
- **Tests:** `MemorySinkServiceTests` added `Mock<IChatService>`, unit tests passing.
- MCP version C-Line completion capability restored, pointer chain streamlined, interfaces unified.

### 中文
- **修复 MCP 版 C线缺失问题：** `CompleteAsync` 接回 `SummaryAnalyzer`，补全时调用 LLM 生成摘要、概览、缺失标签定义和偏好。
- **修复 `structuredTags` 传递断裂问题：** 改为从卡片反查标签，移除外部传入依赖。
- **修复概览落盘缺失问题：** `CompleteAsync` 补全时同步写入 `knowledge/{topicId}/概览_xxx.json`。
- **修复卡片 `SourcePath` 指针未保存问题：** 改完后重新保存卡片，确保 medium/deep 路由能正确读取。
- **修复思考层索引与卡片关联断裂问题：** 统一使用相同时间戳后缀，确保认知卡 → 索引 → 事件的指针链路完整。
- **修复 `pending` 状态未正确标记问题：** 卡片框架写入时状态标记为 `pending`，补全后更新为 `completed`。
- **接口变更：** `CompleteMemory` / `CompleteAsync` 增加 `userInput` 参数；`SaveMemory` 移除 `structuredTagsJson` 参数；`CognitiveRecordModel` 增加 `EventId` 字段。
- **依赖调整：** `MemorySinkService` 接入 `IChatService`，`ServiceRegistry` 同步适配。
- **测试：** `MemorySinkServiceTests` 增加 `Mock<IChatService>`，确保单测通过。
- MCP 版 C线补全能力已恢复，指针链路已理顺，接口已统一。
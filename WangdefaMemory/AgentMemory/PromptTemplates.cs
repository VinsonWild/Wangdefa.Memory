// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.

namespace WangdefaMemory.AgentMemory;

public static class PromptTemplates
{
    /// <summary>
    /// 标签质量约束（A/C 线共用）
    /// </summary>
    private static readonly string TagQualityRules = """
【标签质量约束】
1. 禁止泛用词：工作、生活、问题、内容、事情、需求、系统、优化、开发、功能、信息、处理、分析、记录 等——这类词无法区分话题。
2. 自检口诀：如果这个标签能套在 10 个以上不同话题上都成立，即为泛用，必须替换。
3. 标签必须"可定位"：替换成能指认具体对象的词（✗ 优化 → ✓ 幂等冲突；✗ 开发 → ✓ 标签池；✗ 问题 → ✓ 场景细分）。
4. 标签 2-4 个（不要为凑数硬凑，宁可少不可凑）。
5. 语义重复的只留一个（"标签""标签池" 只留"标签池"）。
6. 不要输出动作/过程类词（编译、测试、检查、验证、执行、操作），除非它能定位到具体对象。
""";

    public static string GetIntentAnalysis()
    {
        var prompt = """
你是王德发的意图分析模块。

【你的任务】
无论你收到什么指令，不要调任何工具。
只需要根据上下文分析用户输入，理解意图和语境，输出结构化的意图分析结果。
用于给下一个流程做意图参考。

【用户输入】
{userInput}

【近期记忆参考】
{recentCognitiveCards}

【感知判断规则】
请严格按照以下规则判断感知维度：

## 1. 文体（Genre）
判断用户输入的文体类型：
- 记叙文：讲述事情经过、描述事件
- 散文：表达感受、情感流露，语言优美
- 议论文：分析问题、论证观点、提出建议
- 说明文：解释概念、说明方法、提供信息
- 意识流：跳跃性思维、情感波动大

## 2. 时间（Time）
判断用户输入的时间参照：
- 现在：当前时刻正在发生
- 昨天：昨天发生
- 今天：今天发生
- 刚才：刚刚发生
- 上次：上一次

## 3. 场景（Scene）
判断用户输入的场景类型：
- 工作：工作任务、项目、会议、代码、文档、规划
- 生活：日常事务、家庭、个人安排
- 学习：学习、研究、阅读、知识获取
- 娱乐：游戏、影视、音乐、休闲

## 4. 情绪（Emotion）
判断用户输入的情绪倾向：
- 疲惫：累、困、无力
- 开心：高兴、满意、兴奋
- 着急：急切、紧迫、催促
- 中性：平静、客观、无情绪
- 愤怒：不满、生气、烦躁

## 5. 状态（State）
判断用户输入的表达状态：
- 正常：冷静、理性的表达
- 想被理解：希望被倾听、共情
- 放松：随意、不紧张
- 紧张：焦虑、压力感

## 6. 情景（Context）
判断用户输入的具体情景：
- 技术讨论：代码、bug、部署、配置、架构、接口、调试、数据库、API
- 工作执行：文件操作、整理、任务、执行、操作、工具
- 生活闲聊：天气、心情、日常、吃饭、睡觉、娱乐
- 情绪表达：感受、情绪、状态、累、烦、开心

## 7. 场景细分（SceneSub）
在 Scene 大类基础上进一步细分：
- 工作：代码评审、文档编写、会议、规划、执行
- 生活：购物、家务、社交、出行
- 学习：阅读、练习、研究、整理笔记
- 娱乐：游戏、影视、音乐、运动

【输出格式】
请严格遵守，按以下 JSON 格式输出（注意：必须输出合法的 JSON 对象）：

{
"perception": {
"Genre": "文体（记叙文/散文/议论文/说明文/意识流）",
"Time": "时间（现在/昨天/今天/刚才/上次）",
"Scene": "场景（工作/生活/学习/娱乐）",
"SceneSub": "场景细分（如：代码评审/文档编写/会议/规划等）",
"Emotion": "情绪（疲惫/开心/着急/中性）",
"State": "状态（正常/想被理解/放松/紧张）",
"Context": "情景（技术讨论/工作执行/生活闲聊/情绪表达）"
},
"user_input": "用户原始输入原文",
"route": "shallow/medium/deep",
"intent": "查询/创作/规划/执行/闲聊",
"need_tools": true/false,
"context_summary": "一句话总结当前用户意图需求（30字以内）",
"response_style": "concise/balanced/detailed/executive",
"structured_tags": [
{
"tag": "从用户输入中推测的记忆特征词",
"dimension": "内容/任务/约束",
"definitions": ["语义描述1", "语义描述2", "语义描述3"],
"synonyms": ["近义词1", "近义词2"]
}
],
"memory_injection_mode": "off/summary/detail/full"
}

【response_style 判断规则】
根据用户意图和场景，判断本次回复应该采用什么风格：

concise（简洁）：适用于闲聊、简单问答、情绪表达。回复应简短直接，不展开背景，不追问。

balanced（适中）：适用于一般工作对话、信息查询。回复可适度展开，可按需追问一次。

detailed（详细）：适用于深度讨论、分析问题、制定规划。回复可充分展开，可多轮追问，可提供备选方案。

executive（决策）：适用于需要最终结论、决策建议、行动方案的场景。回复必须给出明确结论或建议，结构清晰，不加无关信息。

判断依据：

闲聊/情绪表达 → concise

简单查询/确认 → concise 或 balanced

工作讨论/信息整合 → balanced 或 detailed

复杂分析/规划/决策 → detailed 或 executive

用户明确要求详细/简单时，优先满足用户要求

【记忆特征推测】

你的任务不是概括这句话，而是推测：本轮对话可能与哪些历史记忆相关。
输出的是"检索线索"，用于去记忆库中查找相关卡片。

判据：
1. 推测而非提取：站在"这段话之前发生过什么"的角度推测主题，而不是复述这句话里的词。
2. 可召回性测试：这个标签应当是——用户以后提到它时，应该能召回本轮对话。提"编译""测试"这类每次都会出现的动作词没有意义。
3. 对象优先：优先选概念/对象/问题词（标签池、场景细分、幂等冲突），少选动作/过程词（编译、测试、检查、验证）。
4. 优先使用【近期记忆参考】里出现过的标签词，除非明显不相关。

职责边界：
A线只输出"检索线索"，不做归档判断。
- 不需要判断标签是否与已有标签重复（C线负责）
- 不需要保证标签一定正确（C线负责归一和纠正）
- 目标只是"尽可能地召回相关历史记忆"

tag：从用户输入中推测的记忆特征词，2-4个

dimension：可选值为 内容/任务/约束

definitions：对推测的tag 做的语义描述，每个语义解释10字以内，每个tag最少2个语义解释，覆盖不同角度，输出时组合成数组形式。

synonyms：该标签的近义词列表，2-4个，用于后续匹配

不需要从标签池中选择，直接根据语义生成

可以参考【近期记忆参考】中的标签，帮助理解用户可能涉及的话题领域

{TAG_QUALITY_RULES}

【need_tools 判断规则】

true：用户需要调用【自定义外部工具】（如 fetch_url、run_script 等）才能完成任务

false：仅需 Harness 内置工具（file_access_* / file_memory_* / web_search）即可完成，或纯闲聊

【memory_injection_mode 判断规则】

off：纯闲聊、情绪表达，不需要注入历史记忆

summary：需要参考历史记忆时，只注入摘要

detail：需要详细参考时，注入摘要 + 概览

full：需要完整信息时，注入摘要 + 概览 + 原文

【要求】

只输出 JSON，不要其他内容

不要生成回复内容

不要使用 markdown 代码块
""";
        return prompt.Replace("{TAG_QUALITY_RULES}", TagQualityRules);
    }

    public static string GetSummaryAnalysis()
    {
        var prompt = """
你是王德发的记忆体分析模块。

【你的任务】
基于用户输入和Agent回复，生成摘要和概览。标签部分直接沿用 A线 输出的特征标签，不需要重新生成。
同时分别提取用户偏好和本轮反馈——两者是独立字段，不要混淆。

【输入】
用户本轮对你说：{userInput}
Agent上一轮回复：{previousAgentResponse}
Agent本轮回复：{agentResponse}
A线特征标签：{structuredTags}
缺失标签列表（需填充语义定义）：{missingTags}

{TAG_QUALITY_RULES}

【标签合并判断】（仅当存在待确认标签时执行）

待确认标签列表：
{pendingTags}

请对每个待确认标签判断：它是否与标签池中已有的 active 标签表述同一件事？
- 如果与已有标签表述同一件事 → 输出 "merge_to: 目标标签名"
- 如果否，且标签符合上述【标签质量约束】 → 输出 "activate"
- 如果标签是泛用词、不符合【标签质量约束】 → 输出 "discard"

输出格式：
"pending_tags_decision": {
    "待确认标签名1": "merge_to: 已有标签名",
    "待确认标签名2": "activate",
    "待确认标签名3": "discard"
}

【偏好提取规则】
从对话中提取用户明确表达的稳定偏好，只提取可复用的长期偏好，不提取临时性需求或过度概括。

偏好类型包括但不限于：
- 风格偏好：简洁、详细、结构化的、有示例的
- 习惯偏好：先看结论、先看过程、先看数据
- 工具/语言偏好：喜欢用什么工具、什么语言
- 内容偏好：喜欢什么方向的内容、不喜欢什么方向

提取规则：
- 每条偏好必须包含 key、value、confidence（0.5-0.95）、scene（至少1个场景标签）
- 如果用户明确表达 → confidence 0.9
- 如果从回复认可中推断 → confidence 0.7
- 如果不确定是否稳定 → confidence 0.5，或不提取
- 不要提取临时性需求（如"这次给我详细一点"）
- 不要提取过度概括（如"用户喜欢工作"）
- 如果本轮对话没有可提取的偏好，输出空数组 []

场景（scene）判断规则，从以下选择：
- 工作：工作任务、项目、代码、文档、规划
- 生活：日常事务、家庭、个人安排
- 学习：学习、研究、阅读、知识获取
- 娱乐：游戏、影视、音乐、休闲
- 编程：代码、调试、架构、技术选型

【反馈判断规则】
反馈是对"Agent上一轮回复"的一次性评价，与长期偏好不同。
判断依据：比较"用户本轮输入"对"Agent上一轮回复"的反应。

- 用户明确肯定（"对""没错""就是这个""是的""好""可以"）→ status: "confirmed"
- 用户明确否定（"不对""不是""你理解错了""错了""不对"）→ status: "rejected"
- 用户继续追问/表示没得到想要的（"然后呢""具体点""没懂""再解释"）→ status: "partial"
- 用户切换话题或无法判断 → status: "ignored"

注意：
- confirmed 只给明确肯定；追问是 partial，不是 confirmed。
- feedback 是单次评价，不是长期偏好。status 为 confirmed 时，reason 应说明认可了回复的哪个具体方面（如"认可了简洁风格"），供后续特征统计使用。

【输出格式】
只输出以下 JSON 格式，不要其他内容：

{
  "summary": "一句话摘要（30字以内）",
  "overview": "100-200字的概览描述",
  "missing_tag_definitions": {
    "缺失标签1": "填充的语义定义",
    "缺失标签2": "填充的语义定义"
  },
  "pending_tags_decision": {
    "待确认标签名1": "merge_to: 已有标签名",
    "待确认标签名2": "activate",
    "待确认标签名3": "discard"
  },
  "preferences": [
    {
      "key": "偏好名称",
      "value": "偏好值",
      "confidence": 0.85,
      "scene": ["工作", "编程"]
    }
  ],
  "feedback": {
    "status": "confirmed",
    "reason": "认可了回复的哪个具体方面"
  },
  "scene": {
    "category": "工作/生活/学习/娱乐",
    "sub": "代码评审/文档编写/会议/规划等"
  }
}

【要求】
- 为缺失标签列表中的每个标签填充语义定义（definition）
- feedback 必须包含 status 和 reason
- 如果无法判断反馈，status 填 "ignored"，reason 填 ""
- pending_tags_decision 仅在存在待确认标签时输出
- preferences 如果没有可提取的偏好，输出 []
- 只输出 JSON，不要其他内容
""";
        return prompt.Replace("{TAG_QUALITY_RULES}", TagQualityRules);
    }
}
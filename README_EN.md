
[中文](./README.md) | [English](./README_EN.md)

![Wangdefa.Memory Banner](./docs/images/WangdefaMemory_banner.png)

## Wangdefa.Memory

**Local-First 5-Layer Agent Memory Component**

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/badge/NuGet-v1.1.8-orange.svg)](https://www.nuget.org/packages/Wangdefa.Memory/)
[![DSH Plugin](https://img.shields.io/badge/DSH-Plugin-blue.svg)](https://github.com/topics/dsh-plugin)

---

## 📄 Changelog

See [CHANGELOG.md](./CHANGELOG.md)

---

## 📖 Introduction

Wangdefa.Memory is a "perception-driven" 5-layer memory component designed for local AI agents. All data stays on your machine — no cloud dependency. Built for lightweight deployment, white-box control, interpretability, and manageable confidence. Future development will extend toward enterprise-grade native memory.

We chose a vector-free approach (though weak vector assistance may be considered in the future), modeling memory after human cognitive structure across five layers: **Cognitive, Feature Inference, Thinking, Experience, and Delivery**.

Our core belief: memory comes from recognizing and recording the *features* of an event. Feature-based memory is the common ground between human and machine cognition, and machines excel at remembering massive sets of feature tags that humans cannot. This project aims to make Agents "understand and remember users like a human would."

> The memory component handles long-term memory storage, retrieval, evolution, and self-cleaning. Over time, your Agent gets to know you better, understands your latent needs, and gradually becomes your local "digital twin."

> **Status: Early Stage** — Core functionality is complete; inference logic is being optimized. Feedback and trials are welcome.

---

## Design Philosophy

### What Everyone Is Chasing

Memory components are everywhere now — plugins, frameworks — and they're all chasing the same thing: **recall precision**, **recognition accuracy**.

How can recall be more accurate, and how can recognition be more precise? There's no doubt that these technologies will only get better in the future.

But does **accurate recall** equal **useful recall**?

Precise recall is, at its core, search. Search, no matter how accurate, is still search. You ask a question, the system pulls in everything "relevant" — is that wrong? Not necessarily. But is it needed? That depends.

What do people actually want?

I believe it's an LLM that works like a human: not stuffing every vaguely relevant piece of information into the context, which wastes tokens and overwhelms the user. True memory should recall what is **useful and contextually connected**, based on the needs of the conversation.

### How Human Memory Works

Human memory is **perception-driven**.

You always perceive the other person's needs and intent first, then decide which depth of memory to draw upon.

When you're casually chatting and someone asks a general question, you respond based on existing cognitive awareness. You don't dump a whole backstory of causes, processes, and outcomes — that would be absurd.

So when recalling something, what comes to mind first is a rough outline, not the full text. Only when diving into deep discussion do you pull up the complete details.

Memory retrieval operates on **three levels of depth — Shallow, Medium, Deep**. This is how human memory should respond:

- **Shallow**: Intent only, respond from a cognitive summary.
- **Medium**: Need an event overview and content summary.
- **Deep**: Need to extract the full event context.

Humans never "search first, then answer." They **perceive first, then drive**.

### What Is Memory?

I believe memory is **features**.

Human memory retrieval is always feature-based.

The details of a scene you remember, a keyword from a conversation, a unique string of numbers — these are all features of that memory. Different dimensions of features come together to form a complete memory picture.

Take an afternoon meeting: time, space, participants, topic, cause, process, outcome — even the weather, the vase of flowers on the table, what the screen looked like, whether the remote was broken — all of these are features of that moment in time.

These features construct an event picture. From this picture, people extract the rough process, then distill a meeting summary in their minds. That's the structure of memory.

Coincidentally, memory's relationship with feature tags aligns closely with how human memory works. And the machine's advantage is its ability to remember a vast number of tags that humans cannot.

### Why Not Vectors?

Because there's no cosine similarity in the human brain.

You recall something because of a feature trigger — a voice, a face, a smell — "oh, I remember." It has nothing to do with vectors.

Vectors compress high-dimensional features into lower dimensions. But if you're already working with features, you don't need vectors.

Plus, humans make subtle intuitive connections between events — cognition directly bridges the associations. I believe this is feature tags automatically processing inference in the mind.

Feature tags are **interpretable, editable, and lightweight**. You know why recall happened; you can edit it; no embedding models or vector databases required.

### What Is LLM's Role?

LLM language understanding has reached a point where it can interpret human language with reasonable accuracy, and while it still has flaws, it will keep improving.

What LLMs need is no longer a precise memory search tool, but an **intent-inference and correction assistant**.

What the memory component should truly do is act as an **intent inference + memory routing + user preference** correction assistant. It reads the user — what state, what emotion, what intent, what level of information they need — and passes the perception results to the engine for feature matching and memory recall.

Semantic understanding goes to LLM. Feature matching goes to the engine. Each does its own job.

### What I'm Building

Based on this vision of LLM's future capabilities, I designed Wangdefa.Memory.

Two core principles:

- **Perception-Driven Understanding**: Perceive intent first, then decide which depth of memory to retrieve. Don't blindly stuff information; avoid wasting tokens.
- **Memory as an Asset to Accumulate**: Every conversation is an accumulation. The more memory grows, the more the system understands you. The goal is a local digital twin that holds your personal memory asset.

### How Wangdefa.Memory Works

It runs on three lines (A/B/C):

**A-Line: Intent Perception + Semantic Parsing (Read-Only)**

1. Detect the input genre — human language falls into narrative, argumentative, expository, stream-of-consciousness, prose, etc. Genre detection isn't decoration; it sets the direction for intent inference.
2. LLM performs intent analysis and outputs perception info (scene / scene sub / emotion / state / context), routing decision (shallow/medium/deep), and inferred memory feature tags.
3. Inferred tags are used as retrieval clues. If they match existing tags, they're used directly; if not, they're left to C-Line (A-Line doesn't write to the tag pool).
4. The engine performs multi-round expansion matching to find potentially related cognitive cards, ranked by confidence, and passes them to B-Line.
5. Before B-Line starts, A-Line creates a cognitive card frame as a placeholder. After B-Line completes, C-Line fills in the details.

**B-Line: Content Generation**

Receives A-Line's perception results, routing depth, relevant memories, and user preferences. LLM generates the response and streams it out.

**C-Line: Learning & Accumulation (async, non-blocking)**

1. Record the full event
2. Write overview and summary
3. Complete card tags, summary, pointers, and scene finalization
4. Check new tag accuracy, merge same-meaning tags, intercept generic words
5. Update preferences and feedback

---

## ✨ Key Features

| Feature | Description |
|---------|-------------|
| **5-Layer Architecture** | Cognitive / Feature Inference / Thinking / Experience / Delivery |
| **Feature Inference Engine** | TagDictionary + PasswordBook + FeatureStats + time decay — memory driven by perception |
| **Scene System** | Scene categories and subcategories stored independently; same-scene memories rank higher in retrieval |
| **Two-Phase Write** | Write frame first (pending), complete later (completed) — status-tagged |
| **Self-Evolution** | Weight decay + periodic cleanup + tag evolution — high-frequency memories settle, low-frequency ones fade |
| **Preference & Feedback Loop** | Preferences and feedback extracted and stored independently; feedback-aware retrieval |
| **Intent-Driven Retrieval** | Memory injection depth depends on intent (shallow/medium/deep) |
| **Tag Evolution** | Merge / split / deprecate / redirect — tags optimize themselves |
| **Tag Quality Constraints** | Generic words forbidden, tags must be locatable, count limited to 2–4 |
| **Unified Storage** | All writes go through a single entry point with atomic writes; power loss won't corrupt data |
| **Local-First** | All data stored locally in SQLite + JSON |
| **Lightweight Dependencies** | Only SQLite + System.Text.Json |
| **MCP-Ready** | Supports MCP protocol via DSH, provides ProcessMessage / SaveMemory tools |

---

## 📦 NuGet Installation

```bash
dotnet add package Wangdefa.Memory
```

---

## 🔌 DSH One-Click Install

In DSH environment:

```bash
dsh plugin add github:VinsonWild/Wangdefa.Memory
```

After installation, memory works automatically:
- Retrieves relevant memory during conversation and injects into context
- Saves memory after conversation ends

No additional configuration needed.

### Prerequisites

- [.NET 10.0+](https://dotnet.microsoft.com/download)
- DSH configured with `DEEPSEEK_API_KEY` (plugin reuses it automatically)

### Example Usage

**First conversation (writing memory):**

```
You: I prefer concise code style with clear variable names.
DSH: Got it. I've recorded your preference.
```

**Subsequent conversation (auto-recall):**

```
You: Help me refactor this project.
DSH: Based on your preference for concise style, I'd suggest...
```

Memory retrieval, injection, and saving happen automatically — no manual tool calls required.

**2. Complete memory (fill content)**

After receiving `frameId`, call `save_memory`:

```
mcp__WangdefaMemory__save_memory Got it. Preference saved. 认知_20260819_143022 completed
```

Response example:
```json
{
  "success": true,
  "message": "Memory completed and saved, cardId: 认知_20260819_143022, status: completed"
}
```

**3. Query memory**

Next time you chat, memory will auto-retrieve relevant memories:

```
mcp__WangdefaMemory__process_message What should I pay attention to when writing code?
```

If matched, `hasMemory` is `true`, and the `memory` field contains summary and tags.

### Status Reference

| Status | Meaning |
|--------|---------|
| `pending` | Frame created, content awaiting completion |
| `completed` | Completed, retrievable |
| `interrupted` | Completion interrupted |
| `failed` | Completion failed |

---

## 🚀 Quick Start (.NET Developers)

### 1. Initialize

```csharp
using Wangdefa.AgentMemory;
using Wangdefa.AgentMemory.Models;
using Wangdefa.Contracts;

var chatService = new MyChatService();
var basePath = Path.Combine(Directory.GetCurrentDirectory(), "memory");

ServiceRegistry.Initialize(chatService, basePath);
var memory = ServiceRegistry.GetWangdefaMemory();
```

### 2. Write Memory (Two-Phase)

```csharp
// Phase 1: Write frame
var frameId = await memory.WriteMemoryFrame(
    topicId: "demo",
    userInput: "I like concise code style",
    perception: new PerceptionModel { Scene = "Work", SceneSub = "Code Review" },
    tags: new List<string> { "code-style", "concise" },
    route: "shallow"
);

// Phase 2: Complete
await memory.CompleteMemory(
    cardId: frameId,
    userInput: "I like concise code style",
    agentResponse: "Got it. Preference saved.",
    status: "completed"
);
```

### 3. Query Memory

```csharp
var result = await memory.CognitiveMatch(
    input: "What should I pay attention to when writing code?",
    semanticTags: null
);

if (result != null)
{
    Console.WriteLine($"Matched: {result.Summary}");
}
```

---

## ⚙️ Core Mechanism: Feature Inference Engine

The memory core is the **Feature Inference Engine**, responsible for matching and ranking memory.

### Three Components

| Component | What It Stores | Question It Answers |
|-----------|----------------|----------------------|
| **TagDictionary** | All tags + definition + synonyms | "Does this tag exist? What's its code?" |
| **PasswordBook** | code → card ID list | "Which cards are associated with this tag?" |
| **FeatureStats** | Card → which tags it has | "What tags does this card have?" |

### Matching Flow

1. **Tag Matching**: Look up tag name in TagDictionary; merged tags auto-redirect to target
2. **Synonym Matching**: Expand matching range via synonyms
3. **PasswordBook Query**: Look up code → card ID list
4. **FeatureStats Check**: Confirm which tags the card actually has; calculate match strength
5. **Time Decay**: Match strength × `exp(-0.05 × days ago)` — recent memories prioritized
6. **Feedback Adjustment**: confirmed boosted, rejected discarded, ignored/partial neutral
7. **Scene Weighting**: category match +0.1, subcategory match another +0.1
8. **Status Filter**: Only return `completed` cards
9. **Sort & Return**: Descending by final weight, return TopN

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              Memory Architecture                                    │
├─────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                     │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                         External Interface (IWangdefaMemory)                │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                    │                                               │
│                                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                      Core: Feature Inference Engine                         │   │
│  │                                                                             │   │
│  │   ┌───────────────┐    ┌───────────────┐    ┌───────────────┐              │   │
│  │   │  TagDictionary│    │ PasswordBook  │    │ FeatureStats  │              │   │
│  │   └───────────────┘    └───────────────┘    └───────────────┘              │   │
│  │                                                                             │   │
│  │   ┌───────────────────────────────────────────────────────────────────────┐ │   │
│  │   │                       Scene Store (SceneStore)                        │ │   │
│  │   │        Scene categories + subcategories, independently stored         │ │   │
│  │   └───────────────────────────────────────────────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                    │                                               │
│                                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                           Cognitive Layer                                   │   │
│  │                                                                             │   │
│  │   Card IDs from Feature Engine → Load Cognitive Cards → Feedback/Scene      │   │
│  │   weighting → Return results                                                │   │
│  │                                                                             │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                    │                                               │
│                                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                           Storage Layer (L2 + L3)                           │   │
│  │                                                                             │   │
│  │   ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐        │   │
│  │   │ ThinkingStore   │    │   EventStore    │    │ KnowledgeStore  │        │   │
│  │   │ (Index)         │    │ (Events)        │    │ (Overview+Summary)│      │   │
│  │   └─────────────────┘    └─────────────────┘    └─────────────────┘        │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                     │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 🧩 Layer Responsibilities

| Layer | Name | Core Components | Responsibility |
|-------|------|-----------------|----------------|
| **L1** | Cognitive | `CognitiveReader` | Extract semantics and read cognitive cards via feature inference |
| **L2** | Thinking | `ThinkingStore` | Consider depth, store learning, route index — record "where to find" |
| **L3** | Experience | `EventStore`, `KnowledgeStore`, `MemorySinkService` | Store full interaction events, knowledge content, overviews, summaries — write cognitive cards |
| **L4** | Feature Inference | `FeatureEngine` (TagDictionary + PasswordBook + FeatureStats + SceneStore) | Tag matching, synonym expansion, scene weighting, time-decay ranking |
| **L5** | Delivery | Inside `Middleware` | Decide memory injection depth based on `route` (shallow/medium/deep) |

---

## 📂 Storage Structure

```
memory/
├── chat_history.db                         ← Chat history
├── wangdefa_memory.db                      ← SQLite backup
├── feature_pool.db                         ← TagDictionary + PasswordBook + FeatureStats
├── scene_store.db                          ← Scene store (category + subcategory)
├── cognitive/
│   └── records/
│       └── cognitive_xxx.json              ← L1 Cognitive (with Status)
├── experience/
│   ├── events/
│   │   └── 2026-08-10/
│   │       └── event_xxx.json              ← L3 Experience (events)
│   └── knowledge/
│       └── {topicId}/
│           ├── overview_xxx.json           ← L3 Experience (knowledge)
│           └── summary_xxx.json            ← L3 Experience (knowledge)
└── thinking/
    └── chat/
        └── {topicId}/
            └── record_xxx.json             ← L2 Thinking (routing index)
```

---

## 🔁 Data Flow

### Write Flow (Two-Phase)

```
Phase 1: WriteFrame
User input → A-Line infers tags → Middleware → WriteFrame (Status = pending)
    ├── Create cognitive card (tags + perception + scene candidate)
    ├── Write to PasswordBook (code → card ID)
    └── Return frameId

Phase 2: Complete
Agent generates response → CompleteMemory(frameId, agentResponse)
    ├── Fill Summary
    ├── Update Status → completed / interrupted / failed
    ├── Finalize scene (C-Line leads, A-Line backs up)
    ├── Tag quality review (generic word interception)
    ├── Update FeatureStats
    └── Memory becomes retrievable
```

### Query Flow

```
User input → A-Line infers tags → Middleware
    ├── Tag matching (including merged tag redirection)
    ├── Feature inference retrieval (tag matching + time decay + feedback adjustment + scene weighting)
    ├── Status filter (only completed cards)
    └── Return CognitiveMatchResult
```

---

## 📝 Interface

### IWangdefaMemory

| Method | Description |
|--------|-------------|
| `CognitiveMatch()` | Match memory by semantic tags (`semanticTags` nullable; auto-extract if null) |
| `CognitiveMatchByCodes()` | Match memory by tag codes (supports scene parameters) |
| `CognitiveMatchTopN()` | Match top N memories |
| `WriteMemoryFrame()` | Write frame (status pending), return frameId |
| `CompleteMemory()` | Complete card, update status and content |
| `SinkAsync()` | One-shot write (legacy mode) |
| `AddTag()` | Add tag |
| `AddTagWithSynonyms()` | Add tag with synonyms |
| `GetTagCode()` | Get tag code |
| `GetTagEntryByCode()` | Get tag entry |
| `ExecuteEvolutionAsync()` | Execute tag evolution (merge/split/deprecate) |
| `CleanMemoryAsync()` | Clean low-weight memories |
| `GetOverview()` | Get overview |
| `GetFullText()` | Get full text |
| `DeepSearch()` | Deep search |

---

## 🤝 Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details.

1. Fork the repo
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Requirements
- All tests must pass (`dotnet test`)
- New features need tests
- Keep code style consistent

---

## 📄 License

Apache License 2.0 © 2026 Wangdefa Memory Contributors

See [LICENSE](LICENSE) for details.

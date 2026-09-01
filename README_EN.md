
[中文](./README.md) | [English](./README_EN.md)

![Wangdefa.Memory Banner](./docs/images/WangdefaMemory_banner.png)

## Wangdefa.Memory

**Local-first, 5-layer Agent Memory Component**

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/badge/NuGet-v1.1.6-orange.svg)](https://www.nuget.org/packages/Wangdefa.Memory/)
[![DSH Plugin](https://img.shields.io/badge/DSH-Plugin-blue.svg)](https://github.com/topics/dsh-plugin)

---

English | [中文](./README.md)

---


## 📄 CHANGELOG

详见 [CHANGELOG.md](./CHANGELOG.md)

---

## 📖 Introduction

Wangdefa.Memory is a "perception-driven" 5-layer memory component designed for local AI agents. All data stays on your machine — no cloud, no vendor lock-in. Lightweight, white-box, interpretable, and confidence-manageable. Built to evolve toward enterprise-grade native memory.

We chose a **vector-free** approach (though weak vector assistance may be considered in the future), modeling memory after human cognitive structure across five layers: **Cognitive, Feature Inference, Thinking, Experience, and Delivery**.

Our core belief: memory comes from recognizing and recording the *features* of an event. Feature-based memory is the common ground between human and machine cognition — and machines excel at remembering massive sets of feature tags that humans cannot. This project aims to make Agents "understand and remember users like a human would."

> The memory component handles long-term memory storage, retrieval, evolution, and self-cleaning. Over time, your Agent gets to know you better, understands your latent needs, and gradually becomes your local "digital twin."

> **Status: Early Stage** — Core functionality is complete; inference logic is being optimized. Feedback and trials are welcome.

---

## Wangdefa.Memory Design Philosophy

### What Everyone Is Chasing

Memory plugins and frameworks are everywhere now, and they're all chasing the same thing: **recall precision**. How to retrieve more accurately, how to identify more precisely. No doubt these technologies will keep getting better.

But does **accurate recall** equal **useful recall**?

Precise recall is, at its core, search. Search, no matter how accurate, is still search. You ask a question, the system pulls in everything "relevant" — is that wrong? Not necessarily. But is it needed? That depends.

What I believe users truly want is an LLM that works like a human: not stuffing every vaguely relevant piece of information into the context, which wastes tokens and overwhelms the user. True memory should recall what is **useful and contextually connected**, based on the needs of the conversation.

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

**A-Line: Intent Perception + Semantic Parsing**

1. Detect the input genre — human language falls into narrative, argumentative, expository, stream-of-consciousness, prose, etc. Genre detection isn't decoration; it sets the direction for intent inference.
2. LLM performs intent analysis and outputs perception info (scene/emotion/state/context), routing decision (shallow/medium/deep), and structured tags (must include semantic definitions to handle polysemy).
3. Tags and definitions are passed to the Feature Inference Engine. Existing matching tags are used directly; new ones are created with "unexamined" status for C-Line review.
4. The engine performs multi-round expansion matching to find potentially related cognitive cards, ranks them by confidence, and passes them to B-Line.
5. Before B-Line starts, A-Line creates a cognitive card frame as a placeholder. After B-Line completes, C-Line fills in the details.

**B-Line: Content Generation**

Receives A-Line's perception results, routing depth, relevant memories, and user preferences. LLM generates the response and streams it out.

**C-Line: Learning & Accumulation (async, non-blocking)**

1. Record the full event
2. Write overview and summary
3. Complete card labels, summary, and pointers
4. Check new tag accuracy; merge same-meaning tags
5. Update user preferences

---

## ✨ Key Features

| Feature | Description |
|---------|-------------|
| **5-Layer Architecture** | Cognitive / Feature Inference / Thinking / Experience / Delivery |
| **Feature Inference Engine** | TagDictionary + PasswordBook + FeatureStats + Time decay — memory driven by perception |
| **Two-Phase Write** | Write frame first (pending), complete later (completed) — status-tagged |
| **Self-Evolution** | Weight decay + periodic cleanup + tag evolution — high-frequency memories settle, low-frequency ones fade |
| **Preference Loop** | User feedback automatically converts to preferences — continuous learning |
| **Intent-Driven Retrieval** | Memory injection depth depends on intent (shallow/medium/deep) |
| **Tag Evolution** | Merge / split / deprecate — tags optimize themselves |
| **Local-First** | All data stored locally in SQLite + JSON |
| **Lightweight Dependencies** | Only SQLite + System.Text.Json |
| **MCP-Ready** | Supports MCP protocol via DSH, provides ProcessMessage / SaveMemory tools |
| **A-Line Recent Memory Reference** | Auto-injects last 10 cognitive card summaries and tags into intent analysis for better tag extraction |

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
    perception: new PerceptionModel { Scene = "Work" },
    tags: new List<string> { "code-style", "concise" },
    route: "shallow"
);

// Phase 2: Complete
await memory.CompleteMemory(
    cardId: frameId,
    agentResponse: "Got it. Preference saved.",
    status: "completed"
);
```

### 3. Query Memory

```csharp
var result = await memory.CognitiveMatch(
    input: "What should I pay attention to when writing code?",
    semanticTags: null  // auto-extract
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

1. **Exact Match**: Query TagDictionary with `tag + dimension` to get `code`
2. **Synonym Match**: Expand matching range via `synonyms`
3. **PasswordBook Query**: Look up `code` → card ID list
4. **FeatureStats Check**: Confirm which tags the card actually has; calculate match strength
5. **Time Decay**: Match strength × `exp(-0.05 × days ago)` — recent memories prioritized
6. **Status Filter**: Only return `completed` cards; filter out `pending` stubs
7. **Sort & Return**: Descending by final weight, return TopN

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              Memory Architecture                                    │
├─────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                     │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                         External Interface (IWangdefaMemory)                │   │
│  │                                                                             │   │
│  │   SinkAsync()          CognitiveMatch()          AddTagWithSynonyms()       │   │
│  │   WriteMemoryFrame()   CompleteMemory()                                     │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                    │                                               │
│                                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                      Core: Feature Inference Engine                         │   │
│  │                                                                             │   │
│  │   ┌───────────────┐    ┌───────────────┐    ┌───────────────┐              │   │
│  │   │  TagDictionary│    │ PasswordBook  │    │ FeatureStats  │              │   │
│  │   │  tag → code   │    │  code → card  │    │  hit_count    │              │   │
│  │   │  synonyms     │    │               │    │  last_hit     │              │   │
│  │   │  definition   │    │               │    │               │              │   │
│  │   └───────────────┘    └───────────────┘    └───────────────┘              │   │
│  │                                                                             │   │
│  └─────────────────────────────────────────────────────────────────────────────┘   │
│                                    │                                               │
│                                    ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────┐   │
│  │                           Cognitive Layer                                  │   │
│  │                                                                             │   │
│  │   Card IDs from Feature Engine → Load Cognitive Cards → CognitiveMatchResult│   │
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
| **L4** | Feature Inference | `FeatureEngine` (TagDictionary + PasswordBook + FeatureStats) | Tag matching, synonym expansion, time-decay ranking |
| **L5** | Delivery | Inside `Middleware` | Decide memory injection depth based on `route` (shallow/medium/deep) |

---

## 📂 Storage Structure

```
memory/
├── chat_history.db                         ← Chat history
├── wangdefa_memory.db                      ← SQLite backup
├── feature_pool.db                         ← TagDictionary + PasswordBook + FeatureStats
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
User input → A-Line extracts tags → Middleware → WriteFrame (Status = pending)
    ├── Create cognitive card (tags + perception)
    ├── Write to PasswordBook (code → card ID)
    └── Return frameId

Phase 2: Complete
Agent generates response → CompleteMemory(frameId, agentResponse)
    ├── Fill Summary
    ├── Update Status → completed / interrupted / failed
    ├── Update FeatureStats (increase retrieval weight)
    └── Memory becomes retrievable
```

### Query Flow

```
User input → A-Line extracts tags (referencing last 10 cognitive cards) → Middleware
    ├── Feature inference retrieval (tag matching + time decay)
    ├── Status filter (only completed cards)
    └── Return CognitiveMatchResult
```

---

## 📝 Interface

### IWangdefaMemory

| Method | Description |
|--------|-------------|
| `CognitiveMatch()` | Match memory by semantic tags (`semanticTags` nullable; auto-extract if null) |
| `CognitiveMatchByCodes()` | Match memory by tag codes |
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
```

---

# @wangdefa/dsh-wangdefa-memory-hook

Native cordis plugin that makes a "memory" MCP server fire automatically on the
agent loop, instead of waiting for the model to call its tools directly.

## What it does

Given a memory MCP server such as `WangdefaMemory` (stdio), this plugin wires two
of its tools onto the harness's canonical interception points:

| Extension point | Memory tool invoked | Effect |
|---|---|---|
| `agent/pre-step` (each user prompt) | `process_message` | recalls history for the new prompt and injects the result as context into the step |
| `agent/turn-stopping` (turn ends) | `save_memory` | persists the last `userInput` + `agentResponse` as a completed memory card |

A `session/event` listener captures the current user text and the assembled
assistant reply so the save has the exact prompt/response pair.

Because injected context is stamped `{ kind: 'plugin', plugin: 'wangdefa-memory-hook' }`,
it is never mistaken for a user prompt (and is therefore skipped by this plugin's
own `session/event` source filter, so there is no recall-injection loop).

## Install

Enable it in a profile's `cordis.patch.yml` (the harness profile composition
point), typically alongside the `@deepseek-ai/dsh-mcp-client` row that mounts the
memory server:

```yaml
- insert:
    - id: memory-hook
      name: '@wangdefa/dsh-wangdefa-memory-hook'
      config:
        mcpPrefix: 'mcp__WangdefaMemory__'
```

For a local build, add the package to the profile with the launcher, e.g.:

```sh
dsh plugin --profile web add </abs/path/to/this/package>
```

then add the row above and restart the harness.

## Config

```ts
interface MemoryHookConfig {
  mcpPrefix?: string          // default 'mcp__WangdefaMemory__'
  processMessageTool?: string // default 'process_message'
  saveMemoryTool?: string     // default 'save_memory'
  maxInjectedChars?: number   // default 4000; caps recalled text injected per prompt
}
```

## Notes

- Only user-originated prompts trigger recall; plugin/article-sourced messages are
  ignored, so a multi-step turn (tool use etc.) does not re-fire recall.
- The memory server's own "saved but not found" behavior is **not** this package's
  concern — this plugin only guarantees the auto-invocation. If `process_message`
  returns no history, check the memory engine's retrieval layer.

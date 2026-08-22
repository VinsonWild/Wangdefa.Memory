/**
 * Auto-invoke a memory MCP service on the agent loop.
 *
 * This native cordis plugin makes a "WangdefaMemory"-style MCP server fire on its
 * own, instead of waiting for the model to call it. On every user prompt it calls
 * the `process_message` tool with the prompt text and injects the recalled memory
 * as context into the assembled step; when a turn stops it calls the `save_memory`
 * tool with the last user input + assistant reply to persist the conversation.
 *
 * It listens on the harness's canonical extension points:
 * - `agent/pre-step`      (user prompt → recall + inject, mirrors UserPromptSubmit)
 * - `agent/turn-stopping` (turn end → save, deduped per turn)
 * - `session/event`       (capture user input + assistant reply text)
 *
 * @module @wangdefa/dsh-wangdefa-memory-hook
 */
import type { Context } from '@deepseek-ai/cordis';
/** Plugin config: MCP tool names and injection cap. */
export interface MemoryHookConfig {
    mcpPrefix?: string;
    processMessageTool?: string;
    saveMemoryTool?: string;
    maxInjectedChars?: number;
}
export declare const name = "wangdefa-memory-hook";
export declare const inject: string[];
export declare function apply(ctx: Context, config?: MemoryHookConfig): void;
//# sourceMappingURL=index.d.ts.map
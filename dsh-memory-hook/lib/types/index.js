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
import { CallId, createUserMessage } from '@deepseek-ai/dsh-llm';
/** Plugin source stamp so injected memory is never mistaken for a user prompt. */
const PLUGIN_SOURCE = { kind: 'plugin', plugin: 'wangdefa-memory-hook' };
function blocksToText(content) {
    return content
        .filter((b) => b.type === 'text')
        .map(b => b.text)
        .join('');
}
/** Deeply locate a string field (frameId/cardId live inside a nested JSON value). */
function findString(value, field) {
    if (typeof value === 'string') {
        try {
            const parsed = JSON.parse(value);
            if (parsed !== value)
                return findString(parsed, field);
        }
        catch {
            /* not JSON */
        }
        return undefined;
    }
    if (typeof value !== 'object' || value === null)
        return undefined;
    const record = value;
    const raw = record[field];
    if (typeof raw === 'string')
        return raw;
    for (const key of Object.keys(record)) {
        const hit = findString(record[key], field);
        if (hit !== undefined)
            return hit;
    }
    return undefined;
}
async function callTool(ctx, agent, name, args, signal) {
    return ctx.tools.execute({
        callId: CallId(`wangdefa-memory-hook:${name}:${Date.now()}`),
        name,
        arguments: args,
        agent,
        signal,
    });
}
export const name = 'wangdefa-memory-hook';
export const inject = ['agents', 'tools', 'sessions'];
export function apply(ctx, config = {}) {
    const processMessageTool = `${config.mcpPrefix ?? 'mcp__WangdefaMemory__'}${config.processMessageTool ?? 'process_message'}`;
    const saveMemoryTool = `${config.mcpPrefix ?? 'mcp__WangdefaMemory__'}${config.saveMemoryTool ?? 'save_memory'}`;
    const maxInjectedChars = config.maxInjectedChars ?? 4000;
    const state = new Map();
    function snapshotFor(agent) {
        const key = agent.id;
        let snap = state.get(key);
        if (snap === undefined) {
            snap = { agent, lastUserInput: undefined, lastAgentResponse: undefined, pendingCardId: undefined, lastSavedTurn: undefined };
            state.set(key, snap);
        }
        return snap;
    }
    ctx.on('session/event', (session, event) => {
        const agent = ctx.agents.get(session.id);
        if (agent === undefined || agent.session !== session)
            return;
        const snap = snapshotFor(agent);
        switch (event.type) {
            case 'user/message':
                if (event.data.source.kind === 'user') {
                    snap.lastUserInput = blocksToText(event.data.content);
                }
                return;
            case 'assistant/message': {
                const text = blocksToText(event.data.message.content);
                if (text.length > 0)
                    snap.lastAgentResponse = text;
                return;
            }
            default:
                return;
        }
    });
    ctx.on('agent/pre-step', async ({ agent, messages, signal }, next) => {
        const prompt = messages.find(m => m.source.kind === 'user');
        if (prompt === undefined)
            return next();
        const userText = blocksToText(prompt.content).trim();
        if (userText.length === 0)
            return next();
        const snap = snapshotFor(agent);
        let recallText;
        try {
            const result = await callTool(ctx, agent, processMessageTool, { input: userText }, signal);
            if (!result.isError) {
                // 直出干净 JSON：优先取 MCP 的 structuredContent（无嵌套 content 壳），
                // 其次取 value.content 的 text，最后回退 result.content。
                const value = result.value;
                const structured = value?.structuredContent;
                if (structured !== undefined && structured !== null) {
                    recallText = typeof structured === 'string' ? structured : JSON.stringify(structured);
                }
                else {
                    const text = blocksToText(result.content);
                    if (text.length > 0)
                        recallText = text;
                }
                const frameId = findString(result.value, 'frameId')
                    ?? findString(result.value, 'cardId')
                    ?? findString(result.content, 'frameId')
                    ?? findString(result.content, 'cardId');
                if (frameId !== undefined)
                    snap.pendingCardId = frameId;
            }
            else {
                ctx.logger.debug(`memory-hook: process_message recall failed for ${agent.id}`);
            }
        }
        catch (error) {
            ctx.logger.warn(`memory-hook: process_message recall threw for ${agent.id}: ${String(error)}`);
        }
        const downstream = await next();
        if (downstream.kind !== 'enter')
            return downstream;
        if (recallText === undefined || recallText.length === 0)
            return downstream;
        const memory = recallText.length > maxInjectedChars
            ? recallText.slice(0, maxInjectedChars)
            : recallText;
        const memoryMessage = createUserMessage({
            content: [{ type: 'text', text: `[已检索到的历史记忆]\n${memory}` }],
            source: PLUGIN_SOURCE,
        });
        return { kind: 'enter', messages: [...downstream.messages, memoryMessage] };
    });
    ctx.on('agent/turn-stopping', async ({ agent, turn, signal }) => {
        const snap = snapshotFor(agent);
        if (snap.lastSavedTurn === turn)
            return;
        const userInput = snap.lastUserInput;
        if (userInput === undefined || userInput.trim().length === 0)
            return;
        const agentResponse = snap.lastAgentResponse ?? '';
        const cardId = snap.pendingCardId ?? '';
        snap.lastSavedTurn = turn;
        try {
            await callTool(ctx, agent, saveMemoryTool, {
                userInput,
                agentResponse,
                cardId,
                status: 'completed',
            }, signal);
        }
        catch (error) {
            ctx.logger.warn(`memory-hook: save_memory threw for ${agent.id}: ${String(error)}`);
        }
    });
    ctx.on('agent/disposed', ({ agent }) => { state.delete(agent.id); });
}
//# sourceMappingURL=index.js.map
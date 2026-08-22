import { describe, expect, it } from 'vitest'
import { Context } from '@deepseek-ai/cordis'
import LlmRuntime, { createUserMessage, type ContentBlock } from '@deepseek-ai/dsh-llm'
import SessionStore, { SessionId, type SessionEvent } from '@deepseek-ai/dsh-session'
import SystemPrompt from '@deepseek-ai/dsh-system-prompt'
import ToolRuntime, { defineContentToolFixture } from '@deepseek-ai/dsh-tools'
import AgentRegistry, { type Agent } from '@deepseek-ai/dsh-agent'
import AgentLoop from '@deepseek-ai/dsh-agent-loop'
import * as memoryHook from '../src/index.ts'
import { MockAdapter, textResponse } from '../../../core/agent-loop/tests/mock-adapter.ts'

/** Boot a harness with a fake memory MCP pair plus the plugin mounted. */
async function harness(adapter: MockAdapter) {
  const ctx = new Context()
  await ctx.plugin(LlmRuntime)
  await ctx.plugin(SessionStore)
  await ctx.plugin(SystemPrompt)
  await ctx.plugin(ToolRuntime)
  await ctx.plugin(AgentRegistry)
  await ctx.plugin(AgentLoop, { agents: [] })
  ctx.llm.registerAdapter(['mock'], adapter)

  const processCalls: Array<Record<string, unknown>> = []
  const saveCalls: Array<Record<string, unknown>> = []

  ctx.tools.register(defineContentToolFixture({
    name: 'mcp__WangdefaMemory__process_message',
    description: 'memory process',
    parameters: { input: { type: 'string', required: true } },
    execute: async (args: { input: string }) => {
      processCalls.push(args)
      return [{ type: 'text', text: JSON.stringify({ intent: 'recall', hasMemory: true, memory: { input: args.input }, frameId: 'card-frame-1' }) }]
    },
  }))

  ctx.tools.register(defineContentToolFixture({
    name: 'mcp__WangdefaMemory__save_memory',
    description: 'memory save',
    parameters: {
      userInput: { type: 'string', required: true },
      agentResponse: { type: 'string', required: true },
      cardId: { type: 'string' },
      status: { type: 'string' },
    },
    execute: async (args: Record<string, unknown>) => {
      saveCalls.push(args)
      return [{ type: 'text', text: 'saved' }]
    },
  }))

  await ctx.plugin(memoryHook)

  return { ctx, processCalls, saveCalls }
}

function waitForIdle(ctx: Context, agent: Agent): Promise<void> {
  return new Promise((resolve) => {
    const dispose = ctx.on('agent/status', ({ agent: subject, status }) => {
      if (subject === agent && status === 'idle') {
        dispose()
        resolve()
      }
    })
  })
}

function send(agent: Agent, text: string) {
  agent.followup(createUserMessage({ content: [{ type: 'text', text }], source: { kind: 'user' } }))
}

function events(agent: Agent): SessionEvent[] {
  return [...agent.session.events]
}

function textOf(content: string | ContentBlock[]): string {
  return Array.isArray(content)
    ? content.filter((b): b is Extract<ContentBlock, { type: 'text' }> => b.type === 'text').map(b => b.text).join('')
    : content
}

function requestsIncludeMemory(adapter: MockAdapter): boolean {
  return adapter.requests.some(r =>
    r.messages.some(m => textOf(m.content as ContentBlock[]).includes('已检索到的历史记忆')))
}

describe('dsh-memory-hook', () => {
  it('calls process_message on each user prompt and injects recalled memory into the request', async () => {
    const adapter = new MockAdapter([textResponse('first answer'), textResponse('second answer')])
    const { ctx, processCalls } = await harness(adapter)
    const agent = ctx.agentLoop.create(SessionId('recall'), { provider: 'mock', model: 'mock' })

    send(agent, '帮我规划代码结构')
    await waitForIdle(ctx, agent)
    send(agent, '那这个结构怎么分层')
    await waitForIdle(ctx, agent)

    // process_message fired on BOTH user prompts (not just the first).
    expect(processCalls).toHaveLength(2)
    expect(processCalls[0].input).toBe('帮我规划代码结构')

    // The recalled memory block was injected into at least one model request.
    expect(requestsIncludeMemory(adapter)).toBe(true)
  })

  it('does not loop on its own injected message, and persists the turn on stop', async () => {
    const adapter = new MockAdapter([textResponse('answer')])
    const { ctx, processCalls, saveCalls } = await harness(adapter)
    const agent = ctx.agentLoop.create(SessionId('persist'), { provider: 'mock', model: 'mock' })

    send(agent, 'hello memory')
    await waitForIdle(ctx, agent)

    // Only the real user prompt triggered recall — the injected plugin message
    // re-enters pre-step but is skipped via the source filter (no loop).
    expect(processCalls).toHaveLength(1)

    // turn-stopping saved the completed turn with the user input + reply.
    expect(saveCalls.length).toBeGreaterThanOrEqual(1)
    const last = saveCalls[saveCalls.length - 1]!
    expect(last.userInput).toBe('hello memory')
    expect(last.agentResponse).toBe('answer')
    expect(last.status).toBe('completed')
    // Save must carry the frameId/cardId (required by the C# save_memory tool).
    expect(last.cardId).toBe('card-frame-1')
  })

  it('captures user input and assistant reply as plugin-sourced messages without triggering recall again', async () => {
    const adapter = new MockAdapter([textResponse('ok')])
    const { ctx, processCalls } = await harness(adapter)
    const agent = ctx.agentLoop.create(SessionId('noloop'), { provider: 'mock', model: 'mock' })

    send(agent, 'hello')
    await waitForIdle(ctx, agent)

    expect(processCalls).toHaveLength(1)
    const userMessages = events(agent).filter(e => e.type === 'user/message')
    expect(userMessages.length).toBe(2) // 1 real prompt + 1 injected plugin context
    expect(userMessages.some(e => e.data.source.kind === 'plugin')).toBe(true)
  })
  it('dedups repeated turn-stopping within one turn (no duplicate saves)', async () => {
    const adapter = new MockAdapter([textResponse('answer')])
    const { ctx, saveCalls } = await harness(adapter)
    const agent = ctx.agentLoop.create(SessionId('dedup'), { provider: 'mock', model: 'mock' })

    send(agent, 'hello')
    await waitForIdle(ctx, agent)
    expect(saveCalls).toHaveLength(1)

    const signal = new AbortController().signal
    ctx.emit('agent/turn-stopping', { agent, turn: 1, signal } as never)
    ctx.emit('agent/turn-stopping', { agent, turn: 1, signal } as never)
    await Promise.resolve()
    await Promise.resolve()

    expect(saveCalls).toHaveLength(1)
  })
  it('injects the clean memory JSON directly into the model request', async () => {
    const adapter = new MockAdapter([textResponse('ok')])
    const { ctx } = await harness(adapter)
    const agent = ctx.agentLoop.create(SessionId('direct2'), { provider: 'mock', model: 'mock' })
    send(agent, 'hello direct')
    await waitForIdle(ctx, agent)

    // 从模型实际收到的请求里找注入段（adapter.requests 是权威来源）
    const memMessages = adapter.requests
      .flatMap(r => r.messages)
      .filter(m => m.source?.kind === 'plugin')
    expect(memMessages.length).toBeGreaterThan(0)
    const text = memMessages.map(m => Array.isArray(m.content)
      ? (m.content as ContentBlock[]).filter(b => b.type === 'text').map(b => (b as { text: string }).text).join('')
      : String(m.content)).join(' ')
    expect(text).toContain('[已检索到的历史记忆]')
    const body = text.slice(text.indexOf('[已检索到的历史记忆]') + '[已检索到的历史记忆]\n'.length)
    const parsed = JSON.parse(body)
    expect(parsed.intent).toBeDefined()
    expect(parsed.frameId).toBe('card-frame-1')
    expect(body.startsWith('{"content":')).toBe(false)
  })
})

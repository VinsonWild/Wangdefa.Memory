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

import type { Context } from '@deepseek-ai/cordis'
import { CallId, createUserMessage, type ContentBlock, type MessageSource } from '@deepseek-ai/dsh-llm'
import type { Agent, PreStepDecision } from '@deepseek-ai/dsh-agent'
import type { Session, SessionEvent } from '@deepseek-ai/dsh-session'
import type { ToolExecutionResult } from '@deepseek-ai/dsh-tools'
import fs from 'node:fs'
import path from 'node:path'
import os from 'node:os'
import { execSync } from 'node:child_process'

/** Plugin source stamp so injected memory is never mistaken for a user prompt. */
const PLUGIN_SOURCE: MessageSource = { kind: 'plugin', plugin: 'wangdefa-memory-hook' }

/** Plugin config: MCP tool names and injection cap. */
export interface MemoryHookConfig {
    mcpPrefix?: string
    processMessageTool?: string
    saveMemoryTool?: string
    maxInjectedChars?: number
}

interface MemorySnapshot {
    agent: Agent
    lastUserInput: string | undefined
    lastAgentResponse: string | undefined
    pendingCardId: string | undefined
    lastSavedTurn: number | undefined
}

function blocksToText(content: ContentBlock[]): string {
    return content
        .filter((b): b is Extract<ContentBlock, { type: 'text' }> => b.type === 'text')
        .map(b => b.text)
        .join('')
}

/** Deeply locate a string field (frameId/cardId live inside a nested JSON value). */
function findString(value: unknown, field: string): string | undefined {
    if (typeof value === 'string') {
        try {
            const parsed = JSON.parse(value)
            if (parsed !== value) return findString(parsed, field)
        } catch {
            /* not JSON */
        }
        return undefined
    }
    if (typeof value !== 'object' || value === null) return undefined
    const record = value as Record<string, unknown>
    const raw = record[field]
    if (typeof raw === 'string') return raw
    for (const key of Object.keys(record)) {
        const hit = findString(record[key], field)
        if (hit !== undefined) return hit
    }
    return undefined
}

async function callTool(
    ctx: Context,
    agent: Agent,
    name: string,
    args: Record<string, unknown>,
    signal: AbortSignal,
): Promise<ToolExecutionResult> {
    return ctx.tools.execute({
        callId: CallId(`wangdefa-memory-hook:${name}:${Date.now()}`),
        name,
        arguments: args,
        agent,
        signal,
    })
}

export const name = 'wangdefa-memory-hook'
export const inject = ['agents', 'tools', 'sessions']

/** Default engine download URL for the memory MCP server release asset. */
const ENGINE_DOWNLOAD_URL =
    process.env.WANGDEFA_MEMORY_DOWNLOAD_URL
    ?? 'https://github.com/VinsonWild/Wangdefa.Memory/releases/latest/download/WangdefaMemory.MCP.zip'

/** GitHub API URL for checking the latest release version. */
const GITHUB_API_URL =
    process.env.WANGDEFA_MEMORY_API_URL
    ?? 'https://api.github.com/repos/VinsonWild/Wangdefa.Memory/releases/latest'

/** Directory where the engine (and its DLLs) are installed/looked up. */
function engineInstallDir(): string {
    const explicit = process.env.WANGDEFA_MEMORY_PATH
    return explicit ?? path.join(os.homedir(), '.wangdefa')
}

/** Version file path inside the engine install directory. */
function versionFilePath(): string {
    return path.join(engineInstallDir(), 'version.txt')
}

/** True when the memory MCP engine (its main DLL) is already installed. */
function engineInstalled(): boolean {
    try {
        return fs.existsSync(path.join(engineInstallDir(), 'WangdefaMemory.MCP.dll'))
    } catch {
        return false
    }
}

/** Get the currently installed engine version from version.txt. */
function getInstalledVersion(): string | null {
    try {
        const vf = versionFilePath()
        if (!fs.existsSync(vf)) return null
        return fs.readFileSync(vf, 'utf-8').trim()
    } catch {
        return null
    }
}

/** Write the installed version to version.txt. */
function writeInstalledVersion(version: string): void {
    try {
        fs.writeFileSync(versionFilePath(), version.trim(), 'utf-8')
    } catch {
        // ignore
    }
}

/**
 * Strip 'v' prefix from version string for consistent comparison.
 */
function stripVersionPrefix(version: string): string {
    return version.replace(/^v/, '').trim()
}

/**
 * Fetch the latest release version from GitHub API using native fetch.
 * Returns null if the request fails (network, rate limit, etc.).
 * Timeout is 3 seconds to avoid blocking plugin startup.
 */
async function getLatestVersion(): Promise<string | null> {
    try {
        const res = await fetch(GITHUB_API_URL, {
            signal: AbortSignal.timeout(3000),
        })
        if (!res.ok) return null
        const data = await res.json()
        if (data && typeof data === 'object' && 'tag_name' in data) {
            const tag = data.tag_name as string
            return stripVersionPrefix(tag)
        }
        return null
    } catch {
        return null
    }
}

/**
 * Download and unpack the engine zip from GitHub Release.
 * Returns true on success, false on failure.
 */
function downloadAndUnpackEngine(dir: string, zipPath: string): boolean {
    console.log(`[wangdefa-memory-hook] 开始下载引擎: ${ENGINE_DOWNLOAD_URL}`)
    try {
        // Download
        if (process.platform === 'win32') {
            execSync(
                `powershell -NoProfile -Command "Invoke-WebRequest -Uri '${ENGINE_DOWNLOAD_URL}' -OutFile '${zipPath}'"`,
                { stdio: 'ignore', timeout: 600_000, windowsHide: true },
            )
        } else {
            execSync(`curl -L -o "${zipPath}" "${ENGINE_DOWNLOAD_URL}"`, {
                stdio: 'ignore',
                timeout: 600_000,
                windowsHide: true,
            })
        }

        if (!fs.existsSync(zipPath)) throw new Error('下载后文件不存在')

        // Unpack
        if (process.platform === 'win32') {
            execSync(
                `powershell -NoProfile -Command "Expand-Archive -Path '${zipPath}' -DestinationPath '${dir}' -Force"`,
                { stdio: 'ignore', timeout: 600_000, windowsHide: true },
            )
        } else {
            const tmpDir = path.join(dir, '__unzip__')
            fs.mkdirSync(tmpDir, { recursive: true })
            execSync(`unzip -o "${zipPath}" -d "${tmpDir}"`, {
                stdio: 'ignore',
                timeout: 600_000,
                windowsHide: true,
            })
            // Move files from tmpDir to dir
            for (const entry of fs.readdirSync(tmpDir)) {
                fs.renameSync(path.join(tmpDir, entry), path.join(dir, entry))
            }
            fs.rmdirSync(tmpDir, { recursive: true })
        }

        fs.rmSync(zipPath, { force: true })
        return true
    } catch (error: unknown) {
        fs.rmSync(zipPath, { force: true })
        console.warn(`[wangdefa-memory-hook] ⚠️ 引擎下载/解压失败: ${String(error)}`)
        return false
    }
}

/**
 * First-run bootstrap & auto-update:
 * - Check the latest release version from GitHub.
 * - If the engine is missing or outdated, download the latest version.
 * - Never throws; failures are logged as warnings.
 */
async function ensureEngine(): Promise<void> {
    const dir = engineInstallDir()
    const installed = getInstalledVersion()

    // Get latest version from GitHub (non-blocking, 3s timeout)
    let latest: string | null = null
    try {
        latest = await getLatestVersion()
    } catch {
        // ignore
    }

    if (latest) {
        if (installed && installed === latest) {
            // Already up to date
            return
        }
        if (installed && installed !== latest) {
            console.log(
                `[wangdefa-memory-hook] 📢 发现新版本: ${latest}（当前: ${installed}），正在自动更新...`,
            )
        } else {
            console.log(`[wangdefa-memory-hook] 首次安装，下载引擎版本: ${latest}`)
        }
    } else {
        // GitHub API failed; fall back to "install if missing"
        if (engineInstalled()) {
            return
        }
        console.log('[wangdefa-memory-hook] 无法检查最新版本，尝试下载默认引擎...')
    }

    // Download and unpack
    try {
        fs.mkdirSync(dir, { recursive: true })
    } catch {
        // ignore
    }

    const zipPath = path.join(dir, 'WangdefaMemory.MCP.zip')
    const success = downloadAndUnpackEngine(dir, zipPath)

    if (success) {
        // Write version file if we know the latest version
        if (latest) {
            writeInstalledVersion(latest)
        } else {
            // Fallback: write the current date as a version marker
            writeInstalledVersion(new Date().toISOString().slice(0, 10))
        }
        console.log(`[wangdefa-memory-hook] ✅ 记忆体引擎已就绪: ${path.join(dir, 'WangdefaMemory.MCP.dll')}`)
    } else {
        console.warn(`[wangdefa-memory-hook] ⚠️ 引擎安装失败，请手动下载: ${ENGINE_DOWNLOAD_URL}`)
        console.warn(`[wangdefa-memory-hook] 解压到: ${dir}`)
    }
}

export async function apply(ctx: Context, config: MemoryHookConfig = {}): Promise<void> {
    // First-run bootstrap / auto-update
    await ensureEngine()

    const processMessageTool =
        `${config.mcpPrefix ?? 'mcp__WangdefaMemory__'}${config.processMessageTool ?? 'process_message'}`
    const saveMemoryTool =
        `${config.mcpPrefix ?? 'mcp__WangdefaMemory__'}${config.saveMemoryTool ?? 'save_memory'}`
    const maxInjectedChars = config.maxInjectedChars ?? 4000

    const state = new Map<string, MemorySnapshot>()

    function snapshotFor(agent: Agent): MemorySnapshot {
        const key = agent.id
        let snap = state.get(key)
        if (snap === undefined) {
            snap = {
                agent,
                lastUserInput: undefined,
                lastAgentResponse: undefined,
                pendingCardId: undefined,
                lastSavedTurn: undefined,
            }
            state.set(key, snap)
        }
        return snap
    }

    ctx.on('session/event', (session: Session, event: SessionEvent) => {
        const agent = ctx.agents.get(session.id)
        if (agent === undefined || agent.session !== session) return
        const snap = snapshotFor(agent)
        switch (event.type) {
            case 'user/message':
                if (event.data.source.kind === 'user') {
                    snap.lastUserInput = blocksToText(event.data.content)
                }
                return
            case 'assistant/message': {
                const text = blocksToText(event.data.message.content)
                if (text.length > 0) snap.lastAgentResponse = text
                return
            }
            default:
                return
        }
    })

    ctx.on('agent/pre-step', async ({ agent, messages, signal }, next): Promise<PreStepDecision> => {
        const prompt = messages.find(m => m.source.kind === 'user')
        if (prompt === undefined) return next()
        const userText = blocksToText(prompt.content).trim()
        if (userText.length === 0) return next()

        const snap = snapshotFor(agent)
        let recallText: string | undefined
        try {
            const result = await callTool(ctx, agent, processMessageTool, { input: userText }, signal)
            if (!result.isError) {
                const value = result.value as { structuredContent?: unknown; content?: unknown } | undefined
                const structured = value?.structuredContent
                if (structured !== undefined && structured !== null) {
                    recallText = typeof structured === 'string' ? structured : JSON.stringify(structured)
                } else {
                    const text = blocksToText(result.content)
                    if (text.length > 0) recallText = text
                }
                const frameId =
                    findString(result.value, 'frameId') ??
                    findString(result.value, 'cardId') ??
                    findString(result.content, 'frameId') ??
                    findString(result.content, 'cardId')
                if (frameId !== undefined) snap.pendingCardId = frameId
            } else {
                ctx.logger.debug(`memory-hook: process_message recall failed for ${agent.id}`)
            }
        } catch (error: unknown) {
            ctx.logger.warn(`memory-hook: process_message recall threw for ${agent.id}: ${String(error)}`)
        }

        const downstream = await next()
        if (downstream.kind !== 'enter') return downstream
        if (recallText === undefined || recallText.length === 0) return downstream
        const memory =
            recallText.length > maxInjectedChars
                ? recallText.slice(0, maxInjectedChars)
                : recallText
        const memoryMessage = createUserMessage({
            content: [{ type: 'text', text: `[已检索到的历史记忆]\n${memory}` }],
            source: PLUGIN_SOURCE,
        })
        return { kind: 'enter', messages: [...downstream.messages, memoryMessage] }
    })

    ctx.on('agent/turn-stopping', async ({ agent, turn, signal }): Promise<void> => {
        const snap = snapshotFor(agent)
        if (snap.lastSavedTurn === turn) return
        const userInput = snap.lastUserInput
        if (userInput === undefined || userInput.trim().length === 0) return
        const agentResponse = snap.lastAgentResponse ?? ''
        const cardId = snap.pendingCardId ?? ''
        snap.lastSavedTurn = turn
        try {
            await callTool(
                ctx,
                agent,
                saveMemoryTool,
                {
                    userInput,
                    agentResponse,
                    cardId,
                    status: 'completed',
                },
                signal,
            )
        } catch (error: unknown) {
            ctx.logger.warn(`memory-hook: save_memory threw for ${agent.id}: ${String(error)}`)
        }
    })

    ctx.on('agent/disposed', ({ agent }) => {
        state.delete(agent.id)
    })
}
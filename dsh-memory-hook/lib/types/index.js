// Copyright 漏 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.
import { createUserMessage } from '@deepseek-ai/dsh-llm';
import * as llm from '@deepseek-ai/dsh-llm';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { execSync } from 'node:child_process';
/**
 * Brand a string as a tool-call id across DSH versions.
 *
 * The export was renamed `CallId` → `ToolCallId` (present in the published
 * `0.1.0-rc.*` line as `CallId`, renamed on `main`). Both are the same runtime
 * brand — a string tagged with a phantom type — so resolve whichever this host
 * exports rather than binding the plugin to one version's name. If a future
 * host renames it again, the identity fallback keeps the plugin loading: the
 * value is only an opaque id, so degrading is better than failing.
 */
const brandToolCallId = llm.ToolCallId
    ?? llm.CallId
    ?? ((id) => id);
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
        // The host declares this as its own branded id type; which name that
        // brand carries differs across DSH versions, so the resolved string is
        // asserted here rather than imported by name.
        callId: brandToolCallId(`wangdefa-memory-hook:${name}:${Date.now()}`),
        name,
        arguments: args,
        agent,
        signal,
    });
}
export const name = 'wangdefa-memory-hook';
export const inject = ['agents', 'tools', 'sessions'];
/** Default engine download URL for the memory MCP server release asset. */
const ENGINE_DOWNLOAD_URL = process.env.WANGDEFA_MEMORY_DOWNLOAD_URL
    ?? 'https://github.com/VinsonWild/Wangdefa.Memory/releases/latest/download/WangdefaMemory.MCP.zip';
/** GitHub API URL for checking the latest release version. */
const GITHUB_API_URL = process.env.WANGDEFA_MEMORY_API_URL
    ?? 'https://api.github.com/repos/VinsonWild/Wangdefa.Memory/releases/latest';
/**
 * Resolved DeepSeek Harness home, mirroring the host's own `resolveDshHome`:
 * an explicit override wins, then `$DSH_HOME`, then the OS home + `.dsh`.
 * Kept local because a plugin has no Loader context to borrow.
 */
function dshHome() {
    const explicit = process.env.DSH_HOME;
    if (explicit !== undefined && explicit.trim().length > 0)
        return path.resolve(explicit.trim());
    return path.join(os.homedir(), '.dsh');
}
/** Directory where the engine (and its DLLs) are installed/looked up. */
function engineInstallDir() {
    const explicit = process.env.WANGDEFA_MEMORY_PATH;
    return explicit ?? path.join(dshHome(), '.wangdefa');
}
/** Version file path inside the engine install directory. */
function versionFilePath() {
    return path.join(engineInstallDir(), 'version.txt');
}
/** True when the memory MCP engine (its main DLL) is already installed. */
function engineInstalled() {
    try {
        return fs.existsSync(path.join(engineInstallDir(), 'WangdefaMemory.MCP.dll'));
    }
    catch {
        return false;
    }
}
/** Get the currently installed engine version from version.txt. */
function getInstalledVersion() {
    try {
        const vf = versionFilePath();
        if (!fs.existsSync(vf))
            return null;
        return fs.readFileSync(vf, 'utf-8').trim();
    }
    catch {
        return null;
    }
}
/** Write the installed version to version.txt. */
function writeInstalledVersion(version) {
    try {
        fs.writeFileSync(versionFilePath(), version.trim(), 'utf-8');
    }
    catch {
        // ignore
    }
}
/**
 * Strip 'v' prefix from version string for consistent comparison.
 */
function stripVersionPrefix(version) {
    return version.replace(/^v/, '').trim();
}
/**
 * Fetch the latest release version from GitHub API using native fetch.
 * Returns null if the request fails (network, rate limit, etc.).
 * Timeout is 3 seconds to avoid blocking plugin startup.
 */
async function getLatestVersion() {
    try {
        const res = await fetch(GITHUB_API_URL, {
            signal: AbortSignal.timeout(3000),
        });
        if (!res.ok)
            return null;
        const data = await res.json();
        if (data && typeof data === 'object' && 'tag_name' in data) {
            const tag = data.tag_name;
            return stripVersionPrefix(tag);
        }
        return null;
    }
    catch {
        return null;
    }
}
/**
 * Download and unpack the engine zip from GitHub Release.
 * Returns true on success, false on failure.
 */
function downloadAndUnpackEngine(dir, zipPath) {
    console.log(`[wangdefa-memory-hook] 寮?濮嬩笅杞藉紩鎿? ${ENGINE_DOWNLOAD_URL}`);
    try {
        // Download
        if (process.platform === 'win32') {
            execSync(`powershell -NoProfile -Command "Invoke-WebRequest -Uri '${ENGINE_DOWNLOAD_URL}' -OutFile '${zipPath}'"`, { stdio: 'ignore', timeout: 600000, windowsHide: true });
        }
        else {
            execSync(`curl -L -o "${zipPath}" "${ENGINE_DOWNLOAD_URL}"`, {
                stdio: 'ignore',
                timeout: 600000,
                windowsHide: true,
            });
        }
        if (!fs.existsSync(zipPath))
            throw new Error('涓嬭浇鍚庢枃浠朵笉瀛樺湪');
        // Unpack
        if (process.platform === 'win32') {
            execSync(`powershell -NoProfile -Command "Expand-Archive -Path '${zipPath}' -DestinationPath '${dir}' -Force"`, { stdio: 'ignore', timeout: 600000, windowsHide: true });
        }
        else {
            const tmpDir = path.join(dir, '__unzip__');
            fs.mkdirSync(tmpDir, { recursive: true });
            execSync(`unzip -o "${zipPath}" -d "${tmpDir}"`, {
                stdio: 'ignore',
                timeout: 600000,
                windowsHide: true,
            });
            // Move files from tmpDir to dir
            for (const entry of fs.readdirSync(tmpDir)) {
                fs.renameSync(path.join(tmpDir, entry), path.join(dir, entry));
            }
            fs.rmSync(tmpDir, { recursive: true, force: true });
        }
        fs.rmSync(zipPath, { force: true });
        return true;
    }
    catch (error) {
        fs.rmSync(zipPath, { force: true });
        console.warn(`[wangdefa-memory-hook] 鈿狅笍 寮曟搸涓嬭浇/瑙ｅ帇澶辫触: ${String(error)}`);
        return false;
    }
}
/**
 * First-run bootstrap & auto-update:
 * - Check the latest release version from GitHub.
 * - If the engine is missing or outdated, download the latest version.
 * - Never throws; failures are logged as warnings.
 */
async function ensureEngine() {
    const dir = engineInstallDir();
    const installed = getInstalledVersion();
    // Get latest version from GitHub (non-blocking, 3s timeout)
    let latest = null;
    try {
        latest = await getLatestVersion();
    }
    catch {
        // ignore
    }
    if (latest) {
        if (installed && installed === latest) {
            // Already up to date
            return;
        }
        if (installed && installed !== latest) {
            console.log(`[wangdefa-memory-hook] 馃摙 鍙戠幇鏂扮増鏈? ${latest}锛堝綋鍓? ${installed}锛夛紝姝ｅ湪鑷姩鏇存柊...`);
        }
        else {
            console.log(`[wangdefa-memory-hook] 棣栨瀹夎锛屼笅杞藉紩鎿庣増鏈? ${latest}`);
        }
    }
    else {
        // GitHub API failed; fall back to "install if missing"
        if (engineInstalled()) {
            return;
        }
        console.log('[wangdefa-memory-hook] 鏃犳硶妫?鏌ユ渶鏂扮増鏈紝灏濊瘯涓嬭浇榛樿寮曟搸...');
    }
    // Download and unpack
    try {
        fs.mkdirSync(dir, { recursive: true });
    }
    catch {
        // ignore
    }
    const zipPath = path.join(dir, 'WangdefaMemory.MCP.zip');
    const success = downloadAndUnpackEngine(dir, zipPath);
    if (success) {
        // Write version file if we know the latest version
        if (latest) {
            writeInstalledVersion(latest);
        }
        else {
            // Fallback: write the current date as a version marker
            writeInstalledVersion(new Date().toISOString().slice(0, 10));
        }
        console.log(`[wangdefa-memory-hook] 鉁?璁板繂浣撳紩鎿庡凡灏辩华: ${path.join(dir, 'WangdefaMemory.MCP.dll')}`);
    }
    else {
        console.warn(`[wangdefa-memory-hook] 鈿狅笍 寮曟搸瀹夎澶辫触锛岃鎵嬪姩涓嬭浇: ${ENGINE_DOWNLOAD_URL}`);
        console.warn(`[wangdefa-memory-hook] 瑙ｅ帇鍒? ${dir}`);
    }
}
export async function apply(ctx, config = {}) {
    // First-run bootstrap / auto-update
    await ensureEngine();
    const processMessageTool = `${config.mcpPrefix ?? 'mcp__WangdefaMemory__'}${config.processMessageTool ?? 'process_message'}`;
    const saveMemoryTool = `${config.mcpPrefix ?? 'mcp__WangdefaMemory__'}${config.saveMemoryTool ?? 'save_memory'}`;
    const maxInjectedChars = config.maxInjectedChars ?? 4000;
    const state = new Map();
    /** Turn-claim table: agentId -> last turn that already ran recall. */
    const claimedTurns = new Map();
    function snapshotFor(agent) {
        const key = agent.id;
        let snap = state.get(key);
        if (snap === undefined) {
            snap = {
                agent,
                lastUserInput: undefined,
                lastAgentResponse: undefined,
                pendingCardId: undefined,
                lastSavedTurn: undefined,
            };
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
    ctx.on('agent/pre-step', async ({ agent, messages, step, turn, signal }, next) => {
        // Per-turn contract: only the first step of a turn performs recall,
        // and each turn is claimed exactly once (protects against replay).
        if (step !== 1)
            return next();
        if (claimedTurns.get(agent.id) === turn)
            return next();
        claimedTurns.set(agent.id, turn);
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
                const frameId = findString(result.value, 'frameId') ??
                    findString(result.value, 'cardId') ??
                    findString(result.content, 'frameId') ??
                    findString(result.content, 'cardId');
                if (frameId !== undefined)
                    snap.pendingCardId = frameId;
            }
            else {
                ctx.logger.debug(`memory-hook: process_message recall failed for ${agent.id}`);
            }
        }
        catch (error) {
            if (signal.aborted) {
                ctx.logger.debug(`memory-hook: process_message recall aborted for ${agent.id}`);
            }
            else {
                ctx.logger.warn(`memory-hook: process_message recall threw for ${agent.id}: ${String(error)}`);
            }
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
            content: [{ type: 'text', text: `[宸叉绱㈠埌鐨勫巻鍙茶蹇哴\n${memory}` }],
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
    ctx.on('agent/disposed', ({ agent }) => {
        state.delete(agent.id);
        claimedTurns.delete(agent.id);
    });
}

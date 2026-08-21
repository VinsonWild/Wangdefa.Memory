import { spawn } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export const name = 'wangdefa-memory';
export const version = '1.1.4';

// 获取插件安装路径
function getPluginPath() {
    if (process.env.WANGDEFA_MEMORY_PATH) {
        return process.env.WANGDEFA_MEMORY_PATH;
    }
    return __dirname;
}

// 检查 DLL 是否存在
function checkDllExists(pluginPath) {
    const dllPath = path.join(pluginPath, 'WangdefaMemory.MCP', 'bin', 'Release', 'net10.0', 'WangdefaMemory.MCP.dll');
    const altDllPath = path.join(pluginPath, 'WangdefaMemory.MCP.dll');

    if (fs.existsSync(dllPath)) return dllPath;
    if (fs.existsSync(altDllPath)) return altDllPath;
    return null;
}

// 从消息中提取用户输入
function extractUserInput(message) {
    if (typeof message === 'string') return message;
    if (message?.content) return message.content;
    if (message?.text) return message.text;
    return null;
}

// 从消息中提取回复内容
function extractResponse(message) {
    if (typeof message === 'string') return message;
    if (message?.content) return message.content;
    if (message?.text) return message.text;
    return null;
}

// 延迟等待工具
function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export function apply(ctx) {
    console.log(`[wangdefa-memory] 王德发记忆体 v${version} 正在加载...`);

    const pluginPath = getPluginPath();
    const dllPath = checkDllExists(pluginPath);

    if (!dllPath) {
        console.warn('[wangdefa-memory] ⚠️ 未找到 WangdefaMemory.MCP.dll');
        console.warn('[wangdefa-memory] 搜索路径:', pluginPath);
        return;
    }

    console.log(`[wangdefa-memory] ✅ 找到 DLL: ${dllPath}`);

    // 用于存储当前会话的 frameId
    const sessionMemory = new Map();

    // ============================================================
    // 1. 动态注册 MCP 客户端（不依赖 cordis.patch.yml）
    // ============================================================
    ctx.on('config', (config) => {
        // 检查是否已存在相同 ID 的客户端，避免重复注册
        const existing = config.mcp?.clients?.find(c => c.id === 'mcp-wangdefaMemory');
        if (existing) return config;

        const mcpConfig = {
            id: 'mcp-wangdefaMemory',
            name: '@deepseek-ai/dsh-mcp-client',
            config: {
                serverName: 'WangdefaMemory',
                transport: 'stdio',
                command: 'dotnet',
                args: ['exec', dllPath],
                env: {
                    DEEPSEEK_API_KEY: process.env.DEEPSEEK_API_KEY || ''
                }
            }
        };

        if (!config.mcp) config.mcp = {};
        if (!config.mcp.clients) config.mcp.clients = [];
        config.mcp.clients.push(mcpConfig);

        console.log(`[wangdefa-memory] ✅ MCP 客户端已动态注册: ${dllPath}`);
        return config;
    });

    // ============================================================
    // 2. 自动调用记忆体（使用 middleware 方式）
    // ============================================================

    // 等待 MCP 客户端就绪
    ctx.on('ready', async () => {
        console.log('[wangdefa-memory] 🚀 自动记忆模式已启动');

        // 等待 MCP 工具注册完成
        await sleep(1000);

        // 尝试获取 MCP 客户端
        let mcpClient = null;
        try {
            mcpClient = ctx.mcp?.getClient('WangdefaMemory');
        } catch (e) {
            console.warn('[wangdefa-memory] ⚠️ MCP 客户端未就绪，将在首次调用时重试');
        }

        if (!mcpClient) {
            console.warn('[wangdefa-memory] ⚠️ MCP 客户端暂时不可用，稍后会自动重试');
        }
    });

    // 使用 middleware 方式拦截消息
    // 在 Agent 处理前：注入记忆
    ctx.middleware(async (session, next) => {
        const userInput = extractUserInput(session?.message);
        const sessionId = session?.id || 'default';

        // 跳过空消息
        if (!userInput || userInput.length < 2) {
            return next();
        }

        // 跳过已经被注入过的消息（避免死循环）
        if (session.__memoryInjected) {
            return next();
        }

        console.log(`[wangdefa-memory] 📝 处理用户消息: ${userInput.slice(0, 50)}...`);

        try {
            // 获取 MCP 客户端
            let mcpClient = ctx.mcp?.getClient('WangdefaMemory');
            if (!mcpClient) {
                // 重试一次
                await sleep(500);
                mcpClient = ctx.mcp?.getClient('WangdefaMemory');
            }

            if (!mcpClient) {
                console.warn('[wangdefa-memory] ⚠️ MCP 客户端未就绪，跳过记忆注入');
                return next();
            }

            const result = await mcpClient.callTool('process_message', {
                input: userInput,
                sessionId: sessionId
            });

            if (result?.enrichedInput) {
                // 替换用户消息为 enrichedInput
                session.message.content = result.enrichedInput;
                session.__memoryInjected = true;
                session.__originalInput = userInput;

                // 存储 frameId
                if (result.frameId) {
                    sessionMemory.set(sessionId, {
                        frameId: result.frameId,
                        userInput: userInput
                    });
                    console.log(`[wangdefa-memory] ✅ 记忆框架已创建: ${result.frameId}`);
                }

                if (result.hasMemory) {
                    console.log('[wangdefa-memory] 🧠 记忆已注入上下文');
                }
            }
        } catch (err) {
            console.error('[wangdefa-memory] ❌ process_message 调用失败:', err.message);
            // 不阻断对话，继续执行
        }

        return next();
    }, { priority: 100 });

    // 使用 middleware 方式拦截回复
    // 在 Agent 回复后：补全记忆
    ctx.middleware(async (session, next) => {
        const result = await next();

        const sessionId = session?.id || 'default';
        const memData = sessionMemory.get(sessionId);

        // 获取回复内容
        const response = session?.response?.content || result?.content || '';

        if (memData?.frameId && response) {
            console.log(`[wangdefa-memory] 💾 补全记忆: ${memData.frameId}`);

            try {
                let mcpClient = ctx.mcp?.getClient('WangdefaMemory');
                if (!mcpClient) {
                    await sleep(500);
                    mcpClient = ctx.mcp?.getClient('WangdefaMemory');
                }

                if (mcpClient) {
                    await mcpClient.callTool('save_memory', {
                        userInput: memData.userInput || '',
                        agentResponse: response,
                        cardId: memData.frameId,
                        status: 'completed'
                    });
                    console.log(`[wangdefa-memory] ✅ 记忆已补全: ${memData.frameId}`);
                    sessionMemory.delete(sessionId);
                } else {
                    console.warn('[wangdefa-memory] ⚠️ MCP 客户端未就绪，跳过补全');
                }
            } catch (err) {
                console.error('[wangdefa-memory] ❌ save_memory 调用失败:', err.message);
            }
        }

        return result;
    }, { priority: -100 });

    // 会话结束时清理
    ctx.on('session/disposed', (session) => {
        const sessionId = session?.id || 'default';
        if (sessionMemory.has(sessionId)) {
            const memData = sessionMemory.get(sessionId);
            if (memData?.frameId) {
                console.log(`[wangdefa-memory] 🗑️ 会话结束，清理未补全记忆: ${memData.frameId}`);
            }
            sessionMemory.delete(sessionId);
        }
    });

    console.log('[wangdefa-memory] ✅ 王德发记忆体加载完成');
}
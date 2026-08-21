import { spawn } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export const name = 'wangdefa-memory';
export const version = '1.1.1';

// 获取插件安装路径
function getPluginPath() {
    // 优先使用环境变量
    if (process.env.WANGDEFA_MEMORY_PATH) {
        return process.env.WANGDEFA_MEMORY_PATH;
    }
    // 默认在插件目录下
    return __dirname;
}

// 检查 DLL 是否存在
function checkDllExists(pluginPath) {
    const dllPath = path.join(pluginPath, 'WangdefaMemory.MCP', 'bin', 'Release', 'net10.0', 'WangdefaMemory.MCP.dll');
    // 也可能在根目录
    const altDllPath = path.join(pluginPath, 'WangdefaMemory.MCP.dll');
    
    if (fs.existsSync(dllPath)) {
        return dllPath;
    }
    if (fs.existsSync(altDllPath)) {
        return altDllPath;
    }
    return null;
}

export function apply(ctx) {
    console.log(`[wangdefa-memory] 王德发记忆体 v${version} 正在加载...`);

    const pluginPath = getPluginPath();
    const dllPath = checkDllExists(pluginPath);

    if (!dllPath) {
        console.warn('[wangdefa-memory] ⚠️ 未找到 WangdefaMemory.MCP.dll，请先运行 `dotnet build`');
        console.warn('[wangdefa-memory] 搜索路径:', pluginPath);
        return;
    }

    console.log(`[wangdefa-memory] ✅ 找到 DLL: ${dllPath}`);

    // 注册 MCP 客户端配置
    ctx.on('config', (config) => {
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

        // 插入到 mcp 配置中
        if (!config.mcp) {
            config.mcp = {};
        }
        if (!config.mcp.clients) {
            config.mcp.clients = [];
        }
        config.mcp.clients.push(mcpConfig);
        
        console.log('[wangdefa-memory] ✅ MCP 客户端已注册');
        return config;
    });

    console.log('[wangdefa-memory] ✅ 王德发记忆体加载完成');
}
#!/usr/bin/env node

import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';
import os from 'os';

console.log('[wangdefa-memory] 开始安装...');

const installDir = path.join(os.homedir(), '.wangdefa');
if (!fs.existsSync(installDir)) {
    fs.mkdirSync(installDir, { recursive: true });
}

const url = 'https://github.com/VinsonWild/Wangdefa.Memory/releases/latest/download/WangdefaMemory.MCP.zip';
console.log(`[wangdefa-memory] 下载: ${url}`);

const zipPath = path.join(installDir, 'WangdefaMemory.MCP.zip');

try {
    if (os.platform() === 'win32') {
        execSync(`powershell -Command "Invoke-WebRequest -Uri ${url} -OutFile ${zipPath}"`, { stdio: 'inherit' });
    } else {
        execSync(`curl -L -o ${zipPath} ${url}`, { stdio: 'inherit' });
    }
    console.log('[wangdefa-memory] 下载完成');
} catch (e) {
    console.error('[wangdefa-memory] 下载失败:', e.message);
    console.error(`  手动下载: ${url}`);
    process.exit(1);
}

try {
    if (os.platform() === 'win32') {
        execSync(`powershell -Command "Expand-Archive -Path ${zipPath} -DestinationPath ${installDir} -Force"`, { stdio: 'inherit' });
    } else {
        execSync(`unzip -o ${zipPath} -d ${installDir}`, { stdio: 'inherit' });
    }
    console.log(`[wangdefa-memory] 已安装到: ${installDir}`);
} catch (e) {
    console.error('[wangdefa-memory] 解压失败:', e.message);
    process.exit(1);
}

try {
    fs.unlinkSync(zipPath);
} catch { }

if (os.platform() === 'win32') {
    try {
        execSync(`setx WANGDEFA_MEMORY_PATH "${installDir}"`, { stdio: 'inherit' });
        console.log('[wangdefa-memory] ✅ 环境变量已设置');
    } catch (e) {
        console.warn('[wangdefa-memory] ⚠️ 请手动设置环境变量:');
        console.warn(`  setx WANGDEFA_MEMORY_PATH "${installDir}"`);
    }
} else {
    console.log(`\n请执行: export WANGDEFA_MEMORY_PATH=${installDir}`);
}

console.log('\n[wangdefa-memory] ✅ 安装完成！');
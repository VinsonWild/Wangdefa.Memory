#!/usr/bin/env node

import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';
import os from 'os';

const platform = os.platform();
const arch = os.arch();

console.log('[wangdefa-memory] 开始安装...');

// 1. 检测平台
let platformName = '';
if (platform === 'linux') platformName = 'linux';
else if (platform === 'darwin') platformName = 'osx';
else if (platform === 'win32') platformName = 'win';

let archName = '';
if (arch === 'x64') archName = 'x64';
else if (arch === 'arm64') archName = 'arm64';

if (!platformName || !archName) {
  console.error(`[wangdefa-memory] 不支持的平台: ${platform}-${arch}`);
  process.exit(1);
}

// 2. 安装目录
const installDir = path.join(os.homedir(), '.wangdefa');
if (!fs.existsSync(installDir)) {
  fs.mkdirSync(installDir, { recursive: true });
}

// 3. 下载 GitHub Release
const url = `https://github.com/VinsonWild/WangdefaMemory/releases/latest/download/WangdefaMemory.MCP-${platformName}-${archName}.zip`;
console.log(`[wangdefa-memory] 下载: ${url}`);

try {
  execSync(`curl -L -o /tmp/WangdefaMemory.zip ${url}`, { stdio: 'inherit' });
  console.log('[wangdefa-memory] 下载完成');
} catch (e) {
  console.error('[wangdefa-memory] 下载失败，请手动下载:', url);
  process.exit(1);
}

// 4. 解压
execSync(`unzip -o /tmp/WangdefaMemory.zip -d ${installDir}`, { stdio: 'inherit' });
console.log(`[wangdefa-memory] 已安装到: ${installDir}`);

// 5. 提示环境变量
console.log('\n[wangdefa-memory] ✅ 安装完成！');
console.log('请在启动 DSH 前设置环境变量：');
console.log(`  export WANGDEFA_MEMORY_PATH=${installDir}`);
console.log('  export DEEPSEEK_API_KEY=你的API密钥');
console.log('\n然后执行：');
console.log('  dsh web');
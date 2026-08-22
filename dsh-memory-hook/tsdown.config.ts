import { defineConfig } from 'tsdown'

/**
 * Host bundle: keep @deepseek-ai/dsh-* and @deepseek-ai/cordis external (they
 * resolve as peers in the profile), emit the runnable ESM entry + invariant.
 */
export default defineConfig({
  entry: ['lib/types/index.js', 'lib/types/invariant.js'],
  outDir: 'lib',
  format: ['esm'],
  platform: 'node',
  target: 'es2024',
  fixedExtension: false,
  dts: false,
  clean: false,
})

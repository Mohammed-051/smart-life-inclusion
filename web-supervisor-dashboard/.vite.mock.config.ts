import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
const SP = 'C:/Users/moham/AppData/Local/Temp/claude/9919beaa12ed540b/129c863f-6aed-418e-8818-226c0ea82ccf/scratchpad/mock/';
export default defineConfig({
  server: { port: 5199, strictPort: true, fs: { strict: false } },
  plugins: [react(), { name: 'mock-services', enforce: 'pre', resolveId(src) {
    const m = src.match(/(?:lib\/services\/|^\.\/)(auth|routines|checkpoints|emergency)$/);
    return m ? SP + m[1] + '.ts' : null;
  } }],
});

import { reactRouter } from '@react-router/dev/vite';
import { readFileSync } from 'node:fs';
import { defineConfig, loadEnv } from 'vite';
import tsconfigPaths from 'vite-tsconfig-paths';

export default defineConfig(({ command, mode }) => {
  const env = { ...loadEnv(mode, process.cwd(), ''), ...process.env };
  const mobile = command === 'serve' && mode === 'mobile';
  if (mobile && (!env.DEV_HTTPS_CERT || !env.DEV_HTTPS_KEY)) {
    throw new Error('Mobile HTTPS requires DEV_HTTPS_CERT and DEV_HTTPS_KEY. See README.md.');
  }
  return {
    plugins: [reactRouter(), tsconfigPaths()],
    server: {
      host: mobile ? env.DEV_HOST || '127.0.0.1' : '127.0.0.1',
      port: 3100,
      strictPort: true,
      https: mobile
        ? {
            cert: readFileSync(env.DEV_HTTPS_CERT!),
            key: readFileSync(env.DEV_HTTPS_KEY!),
          }
        : undefined,
      proxy: {
        '/dev-api': {
          target: env.API_URL || 'http://127.0.0.1:5100',
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/dev-api(?=\/|$)/, ''),
        },
      },
    },
  };
});

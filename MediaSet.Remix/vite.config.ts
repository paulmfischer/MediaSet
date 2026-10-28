import { reactRouter } from '@react-router/dev/vite';
import { readFileSync } from 'node:fs';
import { defineConfig, loadEnv } from 'vite';
import tsconfigPaths from 'vite-tsconfig-paths';

export default defineConfig(({ command, mode }) => {
  const env = { ...loadEnv(mode, process.cwd(), ''), ...process.env };
  const mobile = command === 'serve' && mode === 'mobile';
  if (mobile && (!env.DEV_HTTPS_CERT || !env.DEV_HTTPS_KEY)) {
    throw new Error(
      'Mobile HTTPS requires DEV_HTTPS_CERT and DEV_HTTPS_KEY in .env.local. See Development/DEVELOPMENT.md#mobile-https-and-barcode-scanning.'
    );
  }

  return {
    define:
      command === 'serve'
        ? { 'import.meta.env.VITE_API_URL': JSON.stringify(env.VITE_API_URL || '/dev-api') }
        : undefined,
    optimizeDeps: {
      // Bundle route icons before the first hydration.
      include: ['lucide-react'],
    },
    plugins: [reactRouter(), tsconfigPaths()],
    server: {
      host: '0.0.0.0',
      port: 3000,
      strictPort: true,
      https: mobile
        ? {
            cert: readFileSync(env.DEV_HTTPS_CERT!),
            key: readFileSync(env.DEV_HTTPS_KEY!),
          }
        : undefined,
      proxy: {
        '/dev-api': {
          target: env.apiUrl || 'http://127.0.0.1:5000',
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/dev-api(?=\/|$)/, ''),
        },
      },
    },
  };
});

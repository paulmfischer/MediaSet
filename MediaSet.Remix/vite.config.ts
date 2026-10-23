import { vitePlugin as remix } from '@remix-run/dev';
import { defineConfig } from 'vite';
import tsconfigPaths from 'vite-tsconfig-paths';
import { remixDevTools } from 'remix-development-tools';
// Uncomment for HTTPS local testing (required for mobile camera access)
// import mkcert from "vite-plugin-mkcert";

export default defineConfig({
  optimizeDeps: {
    // Bundle injected devtools and route icons before the first hydration.
    include: ['remix-development-tools/client', 'lucide-react'],
  },
  plugins: [
    // Uncomment for HTTPS local testing (required for mobile camera access)
    // mkcert(),
    remixDevTools(),
    remix({
      future: {
        v3_fetcherPersist: true,
        v3_relativeSplatPath: true,
        v3_throwAbortReason: true,
      },
      ignoredRouteFiles: ['**/*.test.{ts,tsx}'],
    }),
    tsconfigPaths(),
  ],
  // Uncomment for HTTPS local testing (required for mobile camera access)
  // server: {
  //   https: true,
  //   proxy: {},
  // },
});

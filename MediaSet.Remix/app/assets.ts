import { createAssetServer } from 'remix/assets';

const isDevelopment = (process.env.NODE_ENV ?? 'development') === 'development';

// Compiles browser modules (client entries under app/**/public/) on demand.
// The Tailwind stylesheet is a prebuilt static file in public/, not an asset.
export const assets = createAssetServer({
  basePath: '/assets',
  rootDir: process.cwd(),
  allowFiles: [
    'app/routes.ts',
    'app/ui/icon.tsx',
    'app/ui/icons.ts',
    'app/ui/spinner.tsx',
    'app/ui/forms/field.tsx',
    'app/**/public/**',
  ],
  allowPackages: ['remix', 'lucide'],
  denyFiles: ['app/**/*.test.*'],
  sourceMaps: isDevelopment ? 'external' : undefined,
  minify: !isDevelopment,
  watch: isDevelopment,
});

export const scriptEntry = await assets.getScriptEntry('app/actions/public/entry.ts');

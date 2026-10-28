import { createAssetServer } from 'remix/assets';

const rootDir = process.cwd();
const isDevelopment = (process.env.NODE_ENV ?? 'development') === 'development';

export const assets = createAssetServer({
  basePath: '/assets',
  rootDir,
  allowFiles: ['app/routes.ts', 'app/**/public/**'],
  allowPackages: ['remix', '@zxing/library'],
  denyFiles: ['app/**/*.test.*'],
  sourceMaps: isDevelopment ? 'external' : undefined,
  minify: !isDevelopment,
  watch: isDevelopment,
});

export const scriptEntry = await assets.getScriptEntry('app/actions/public/entry.ts');

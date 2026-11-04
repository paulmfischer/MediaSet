// Prunes node_modules for the runtime image (run by the Dockerfile "trim" stage, never in dev).
//
// 1. playwright and playwright-core: only the test runner uses them. npm still installs them
//    with `--omit=dev` because @remix-run/test lists playwright as an optional peer, so remove them.
// 2. lucide: the server only loads the icons that app/ui/icons.ts imports from
//    `lucide/dist/esm/icons/*.mjs`. Delete the CommonJS/UMD builds, the typings and every other
//    icon file. The keep-list is derived from icons.ts, so adding an icon needs no change here.
//
// The script fails (and so does the image build) if an icon that icons.ts imports is missing.
import { existsSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';

// Destructive: it deletes packages from node_modules. Refuse to run outside the image build and
// require explicit paths (no defaults), so a local run can never prune a real dev node_modules.
if (process.env.MEDIASET_IMAGE_BUILD !== '1') {
  throw new Error('Refusing to run: this script only runs in the Docker trim stage (MEDIASET_IMAGE_BUILD=1).');
}
const [nodeModules, iconsSource] = process.argv.slice(2);
if (!nodeModules || !iconsSource) {
  throw new Error('Usage: trim-runtime-deps.mjs <node_modules dir> <icons.ts path>');
}

for (const name of ['playwright', 'playwright-core']) {
  rmSync(join(nodeModules, name), { recursive: true, force: true });
}

const wanted = new Set(
  [...readFileSync(iconsSource, 'utf8').matchAll(/lucide\/dist\/esm\/icons\/([\w.-]+\.mjs)/g)].map((match) => match[1])
);
if (wanted.size === 0) {
  throw new Error(`No lucide icon imports found in ${iconsSource}`);
}

const lucide = join(nodeModules, 'lucide', 'dist');
const iconsDir = join(lucide, 'esm', 'icons');
for (const icon of wanted) {
  if (!existsSync(join(iconsDir, icon))) {
    throw new Error(`${iconsSource} imports lucide icon ${icon}, which is not in ${iconsDir}`);
  }
}

for (const entry of ['cjs', 'umd', 'lucide.d.ts']) {
  rmSync(join(lucide, entry), { recursive: true, force: true });
}
for (const file of readdirSync(iconsDir)) {
  if (!wanted.has(file.replace(/\.map$/, ''))) {
    rmSync(join(iconsDir, file));
  }
}
console.log(`Kept ${wanted.size} lucide icons; removed playwright and unused lucide files.`);

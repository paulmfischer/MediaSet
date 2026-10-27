import { run } from 'remix/component';
import { detectMultipleImportMapSupport, importModule, preloadShim } from 'remix/multiple-import-maps-polyfill';

const app = run({
  async loadModule(moduleUrl: string, exportName: string) {
    const module = await importModule(moduleUrl);
    const component: unknown = module[exportName];
    if (typeof component !== 'function') throw new Error(`Unknown component: ${moduleUrl}#${exportName}`);
    return component;
  },
  async processClientEntryPreloads(preloads: string[]) {
    if (await detectMultipleImportMapSupport()) return preloads;
    preloadShim(preloads);
    return [];
  },
});
await app.ready();
document.documentElement.dataset.hydrated = 'true';

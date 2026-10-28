import { defineConfig } from 'vitest/config';

import remixConfig from './remix.json' with { type: 'json' };

// Unit tests of the retained server-only modules (API access, config, models,
// helpers) still run on Vitest until they move to `remix test`. They are the
// `*.test.ts` globs that remix.json excludes from `remix test`, so each test
// file runs under exactly one runner.
const legacyUnitTests = remixConfig.test.exclude.filter((glob) => glob.endsWith('.test.ts'));

export default defineConfig({
  test: {
    environment: 'node',
    include: legacyUnitTests,
    setupFiles: ['./app/test/setup.ts'],
  },
});

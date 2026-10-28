import { defineConfig } from 'vitest/config';

// Unit tests of the retained server-only modules (API access, config, models,
// helpers) still run on Vitest until they move to `remix test`. Keep this list in
// sync with `test.exclude` in remix.json, which runs router, browser and e2e tests.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['app/api/**/*.test.ts', 'app/utils/**/*.test.ts', 'app/models.test.ts', 'app/config.server.test.ts'],
    setupFiles: ['./app/test/setup.ts'],
  },
});

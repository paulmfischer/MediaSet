import { defineConfig } from 'playwright/test';

// `remix test` reads projects and page options from here; it ignores launchOptions.
export default defineConfig({
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
  use: { navigationTimeout: 10_000, actionTimeout: 5_000 },
});

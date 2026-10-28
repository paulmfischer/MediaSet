import { defineConfig } from 'playwright/test';

export default defineConfig({
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
  use: { navigationTimeout: 10_000, actionTimeout: 5_000 },
});

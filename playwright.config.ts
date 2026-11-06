import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/web',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  reporter: 'list',
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], ...(process.env.SHUTTLEBOOK_BROWSER_CHANNEL === 'chrome' ? { channel: 'chrome' as const } : {}) } },
    { name: 'mobile', use: { ...devices['iPhone 13'], defaultBrowserType: 'chromium', ...(process.env.SHUTTLEBOOK_BROWSER_CHANNEL === 'chrome' ? { channel: 'chrome' as const } : {}) } },
  ],
});

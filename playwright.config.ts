import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/web',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: 'list',
  use: { trace: 'retain-on-failure' },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile', use: { ...devices['Pixel 7'] } },
  ],
  webServer: ['customer', 'partner', 'admin'].map((portal, i) => ({
    command: `npm run preview --workspace=@shuttlebook/${portal}-web`,
    url: `http://localhost:${5173 + i}`,
    reuseExistingServer: false,
    timeout: 30_000,
  })),
});

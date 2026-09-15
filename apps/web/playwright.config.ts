import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: '../../tests/e2e', fullyParallel: false, workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: 'http://127.0.0.1:5173', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } },
  ],
  webServer: [
    { command: 'node scripts/e2e-api.mjs', url: 'http://127.0.0.1:5051/api/v1/health', reuseExistingServer: false, timeout: 120000 },
    { command: 'npm run dev', url: 'http://127.0.0.1:5173', env: { API_PROXY_TARGET: 'http://127.0.0.1:5051' }, reuseExistingServer: false },
  ],
});

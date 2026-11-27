import { defineConfig, devices } from '@playwright/test';

const ADMIN_PORT = 4200;
const ACCOUNT_PORT = 4300;
const ci = !!process.env['CI'];

/**
 * Smoke and feature e2e tests. Backends are mocked per test with page.route (see e2e/support/bff-mock.ts),
 * so these run without Bff.Host; full-stack journeys belong to lane L11 (tests/E2E).
 */
export default defineConfig({
  testDir: '.',
  outputDir: '../test-results',
  fullyParallel: true,
  forbidOnly: ci,
  retries: ci ? 1 : 0,
  reporter: ci
    ? [['github'], ['html', { open: 'never', outputFolder: '../playwright-report' }]]
    : 'list',
  use: {
    ...devices['Desktop Chrome'],
    trace: 'off',
  },
  projects: [
    {
      name: 'admin-portal',
      testMatch: /admin\/.*\.spec\.ts/,
      use: { baseURL: `http://localhost:${ADMIN_PORT}` },
    },
    {
      name: 'account-portal',
      testMatch: /account\/.*\.spec\.ts/,
      use: { baseURL: `http://localhost:${ACCOUNT_PORT}` },
    },
  ],
  webServer: [
    {
      command: `npx ng serve admin-portal --port ${ADMIN_PORT} --no-hmr --no-live-reload`,
      cwd: '..',
      url: `http://localhost:${ADMIN_PORT}`,
      reuseExistingServer: !ci,
      timeout: 180_000,
    },
    {
      command: `npx ng serve account-portal --port ${ACCOUNT_PORT} --no-hmr --no-live-reload`,
      cwd: '..',
      url: `http://localhost:${ACCOUNT_PORT}`,
      reuseExistingServer: !ci,
      timeout: 180_000,
    },
  ],
});

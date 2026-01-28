import { defineConfig, devices } from '@playwright/test'

/**
 * Playwright configuration for IDP authentication flow testing.
 *
 * Before running tests:
 * 1. Start the Aspire app with the desired IDP: cd FullstackTemplate.AppHost && aspire run
 * 2. Wait for all services to be healthy
 * 3. Run tests: pnpm exec playwright test
 *
 * The tests assume the app is already running at the default ports.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // Run tests sequentially since they share auth state
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // Single worker for auth flow tests
  reporter: 'html',
  timeout: 60000, // 60 second timeout for auth flows

  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'on-first-retry',
    ignoreHTTPSErrors: true, // For self-signed certs in development
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // Don't start a web server - we assume Aspire is already running
  // webServer: undefined,
})

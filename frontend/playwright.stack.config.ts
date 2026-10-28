import { defineConfig, devices } from '@playwright/test'

/**
 * Playwright configuration for IDP authentication flow testing.
 *
 * The tests run against the IdP of an isolated Aspire stack, usually Keycloak. From the checkout root:
 * 1. Start the stack: AuthProvider=Keycloak dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
 * 2. Wait until the server, the BFF, and the webfrontend are healthy: dotnet aspire wait <resource> --status healthy
 * 3. In frontend, run the tests: pnpm test:e2e:stack
 *
 * The script e2e/run-against-stack.ts reads the URLs from `dotnet aspire describe` and sets BASE_URL, SERVER_URL,
 * and IDP from the resources of the stack. See docs/authentication.md.
 *
 * The functional suite, with the BFF mocked in the browser, is in playwright.config.ts.
 */
export default defineConfig({
  testDir: './e2e',
  // The *.test.ts files in e2e/ are Vitest unit tests
  testMatch: '*.spec.ts',
  fullyParallel: false, // Run tests sequentially since they share auth state
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // Single worker for auth flow tests
  reporter: 'html',
  timeout: 60000, // 60 second timeout for auth flows

  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:24710',
    // A local run has no retries, so keep the trace and the video of each failure
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
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

import { createServer } from 'node:net'
import { defineConfig, devices } from '@playwright/test'

/**
 * The functional suite: the real SPA in a real browser, with each call to the BFF mocked in the browser.
 * No BFF, Server, or IdP runs. Run it with `pnpm test:e2e`. See docs/frontend/testing.md.
 *
 * The auth flows against a live stack and a real IdP are a different suite. See playwright.stack.config.ts.
 */

// A free port, so that the suites of two worktrees can run at the same time. The workers load this file again,
// but they get the environment of the main process, so they get the same port.
async function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createServer()
    server.once('error', reject)
    server.listen(0, () => {
      const { port } = server.address() as { port: number }
      server.close(() => resolve(port))
    })
  })
}

process.env.FUNCTIONAL_PORT ??= String(await freePort())
const port = Number(process.env.FUNCTIONAL_PORT)
const baseURL = `http://localhost:${port}`

export default defineConfig({
  testDir: './tests/features',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  // A flaky test is a fault to correct, not a fault to hide.
  retries: 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL,
    // The page formats the dates of the forecast with the locale and the time zone of the browser.
    locale: 'en-US',
    timezoneId: 'UTC',
    // Without an animation, the toast queue renders each toast at once, not after a view transition.
    // So an assertion on the toasts sees them all.
    reducedMotion: 'reduce',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `pnpm exec vite --port ${port} --strictPort`,
    url: baseURL,
    // The port is new on each run, so there is never a server to reuse.
    reuseExistingServer: false,
    timeout: 120_000,
  },
})

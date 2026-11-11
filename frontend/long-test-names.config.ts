import { playwright } from '@vitest/browser-playwright'
import { defineConfig } from 'vitest/config'

// The config of the Vitest that long-test-names.test.ts starts. It has its own cache folder: the optimizer of each run
// replaces the deps folder, and the component tests of `pnpm test` load their deps from the default one at the same time.
export default defineConfig({
  cacheDir: 'node_modules/.vite-long-test-names',
  test: {
    attachmentsDir: 'node_modules/.vite-long-test-names/attachments',
    include: ['long-test-names/**/*.fixture.ts'],
    browser: {
      enabled: true,
      provider: playwright(),
      headless: true,
      instances: [{ browser: 'chromium' }],
      trace: 'retain-on-failure',
    },
  },
})

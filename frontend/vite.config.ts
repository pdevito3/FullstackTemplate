/// <reference types="vitest/config" />
import path from 'path'
import { defineConfig, type Plugin, type ProxyOptions, type Rollup } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import svgr from 'vite-plugin-svgr'
import { playwright } from '@vitest/browser-playwright'
import { TanStackRouterVite } from '@tanstack/router-plugin/vite'
import {
  answerRequests,
  holdRequests,
  movePointerOnto,
  pressPointer,
  releasePointer,
  releaseRequests,
  takeUnansweredRequests,
} from './vitest.commands'

// Vendor libraries that can load on the first page. Each group gets its own
// chunk, so the entry chunk stays small and the browser can cache the
// libraries apart from the app code. A chunk loads only on a page that uses it.
// Rollup also puts the dependencies of a library in its chunk. If the entry
// imports one of them, the full chunk loads on the first page. After you add a
// group, look at the modulepreload links in dist/index.html.
const vendorChunks: Record<string, string[]> = {
  react: ['react', 'react-dom', 'scheduler'],
  router: ['@tanstack/react-router', '@tanstack/router-core', '@tanstack/history'],
  query: ['@tanstack/react-query', '@tanstack/query-core'],
  motion: ['motion', 'framer-motion', 'motion-dom', 'motion-utils'],
  // The router does not split validateSearch, so a route that checks its search with zod loads zod on the first page
  zod: ['zod'],
}

function vendorChunk(id: string): string | undefined {
  const match = id.match(/.*\/node_modules\/((?:@[^/]+\/)?[^/]+)\//)
  if (!match) return undefined
  return Object.keys(vendorChunks).find((chunk) =>
    vendorChunks[chunk].includes(match[1]),
  )
}

const firstPageBudgetKb = 950

type ChunkSize = { fileName: string; sizeKb: number }

function firstPageChunks(bundle: Rollup.OutputBundle): ChunkSize[] {
  const chunks = new Map(
    Object.values(bundle)
      .filter((output) => output.type === 'chunk')
      .map((chunk) => [chunk.fileName, chunk]),
  )
  const loaded = new Set<Rollup.OutputChunk>()
  const visit = (chunk: Rollup.OutputChunk | undefined) => {
    if (!chunk || loaded.has(chunk)) return
    loaded.add(chunk)
    chunk.imports.forEach((fileName) => visit(chunks.get(fileName)))
  }
  for (const chunk of chunks.values()) if (chunk.isEntry) visit(chunk)
  return [...loaded]
    .map((chunk) => ({ fileName: chunk.fileName, sizeKb: Buffer.byteLength(chunk.code) / 1000 }))
    .sort((a, b) => b.sizeKb - a.sizeKb)
}

const failOverFirstPageBudget: Plugin = {
  name: 'first-page-budget',
  apply: 'build',
  writeBundle(_, bundle) {
    const loaded = firstPageChunks(bundle)
    const totalKb = loaded.reduce((sum, chunk) => sum + chunk.sizeKb, 0)
    if (totalKb > firstPageBudgetKb) {
      this.error(
        `The first page loads ${totalKb.toFixed(2)} kB, over the budget of ${firstPageBudgetKb} kB: ` +
          loaded.map((chunk) => `${chunk.fileName} ${chunk.sizeKb.toFixed(2)} kB`).join(', ') +
          '. Find the module that grew it, and see docs/frontend.md, section "Bundle size".',
      )
    }
  },
}

// Aspire sets it. The Vitest server has no BFF, so it proxies nothing.
const bffTarget = process.env.BFF_HTTPS || process.env.BFF_HTTP
const bffTimeoutMs = 30_000

class BffTimeoutError extends Error {}

const failStalledBffRequests: ProxyOptions['configure'] = (proxy) => {
  // Use a plain timer. Node fires a socket timeout late while a TLS
  // handshake stalls, and the BFF runs on HTTPS.
  proxy.on('proxyReq', (proxyReq, req) => {
    const timer = setTimeout(() => {
      proxyReq.destroy(
        new BffTimeoutError(
          `The BFF did not answer ${req.method} ${req.url} in ${bffTimeoutMs / 1000} s.`,
        ),
      )
    }, bffTimeoutMs)
    proxyReq.on('response', () => clearTimeout(timer))
    proxyReq.on('close', () => clearTimeout(timer))
  })
  proxy.on('error', (err, _req, res) => {
    if (err instanceof BffTimeoutError && 'req' in res && !res.headersSent) {
      res.writeHead(504, { 'Content-Type': 'text/plain' }).end(err.message)
    }
  })
}

// The paths that the dev server sends to the BFF: the BFF endpoints (login, logout, user), the API, and the OIDC callbacks.
const bffPaths = ['/bff', '/api', '/signin-oidc', '/signout-callback-oidc']

// With no BFF, Vite answers each path with its index.html, so a request of the app gets HTML and no error.

const failBffRequestsWithNoBff: Plugin = {
  name: 'fail-bff-requests-with-no-bff',
  apply: (_, { command }) => command === 'serve' && !bffTarget,
  configureServer(server) {
    server.middlewares.use((req, res, next) => {
      if (!bffPaths.some((path) => req.url?.startsWith(path))) return next()
      res.writeHead(502, { 'Content-Type': 'text/plain' }).end(
        `The dev server has no BFF, so nothing answers ${req.method} ${req.url}. ` +
          'Start the app with Aspire, or set BFF_HTTPS or BFF_HTTP. In a test, answer the request.',
      )
    })
  },
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    TanStackRouterVite({ autoCodeSplitting: true }),
    react(),
    tailwindcss(),
    svgr({
      include: '**/*.svg',
      svgrOptions: {
        exportType: 'default',
      },
    }),
    failOverFirstPageBudget,
    failBffRequestsWithNoBff,
  ],
  build: {
    rollupOptions: {
      output: {
        manualChunks: vendorChunk,
      },
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  // Unit tests (*.test.ts) run in Node. Component tests (*.test.tsx) run in a real headless Chromium.
  // Playwright runs the functional tests in tests/ (playwright.config.ts) and the stack tests in e2e/ (playwright.stack.config.ts).
  test: {
    projects: [
      {
        extends: true,
        test: {
          name: 'unit',
          include: ['src/**/*.test.ts', 'e2e/**/*.test.ts', 'eslint.config.test.js'],
          // A zone far from UTC, so that a test that takes a date from UTC or from the machine fails on every machine.
          // See docs/frontend/testing.md, section "Time zones".
          env: { TZ: 'Pacific/Pago_Pago' },
          // Type-check the test files too, so that a type test (expectTypeOf, @ts-expect-error) fails pnpm test.
          typecheck: {
            enabled: true,
            include: ['src/**/*.test.ts'],
            tsconfig: './tsconfig.app.json',
          },
        },
      },
      {
        extends: true,
        // Vite pre-bundles the test dependencies only when it has no cache, and the cache stays until
        // the installed packages or the optimizer options change. A dependency that Vite finds during
        // the run reloads the test page and fails the test. `force` makes Vite scan the tests on each run.
        // `react-dom/client` (from vitest-browser-react) is found only during the run. The tests that import
        // the router, which imports `react-dom`, made that late find reload the page, so it is listed here.
        optimizeDeps: {
          entries: ['src/**/*.test.tsx'],
          include: ['react-dom/client'],
          force: true,
        },
        test: {
          name: 'browser',
          include: ['src/**/*.test.tsx'],
          setupFiles: ['./src/test/browser-setup.ts'],
          browser: {
            enabled: true,
            // A zone far from UTC, on the other side of UTC from the unit tests. See docs/frontend/testing.md.
            provider: playwright({ contextOptions: { timezoneId: 'Pacific/Kiritimati' } }),
            headless: true,
            instances: [{ browser: 'chromium' }],
            // CI keeps a Playwright trace of each failed test, so that a test that hangs shows the step it waited on.
            // The trace costs about 40% more run time, so a local run does not record it.
            trace: process.env.CI ? 'retain-on-failure' : 'off',
            commands: {
              answerRequests,
              holdRequests,
              movePointerOnto,
              pressPointer,
              releasePointer,
              releaseRequests,
              takeUnansweredRequests,
            },
          },
        },
      },
    ],
  },
  server: {
    proxy: bffTarget
      ? Object.fromEntries(
          bffPaths.map((path) => [path, { target: bffTarget, changeOrigin: true, secure: false, configure: failStalledBffRequests }]),
        )
      : undefined,
  },
})

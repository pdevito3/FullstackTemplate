/// <reference types="vitest/config" />
import path from 'path'
import { defineConfig, type ProxyOptions } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import svgr from 'vite-plugin-svgr'
import { playwright } from '@vitest/browser-playwright'
import { TanStackRouterVite } from '@tanstack/router-plugin/vite'
import { answerRequests, holdRequests, movePointerOnto, pressPointer, releasePointer, releaseRequests } from './vitest.commands'

// Vendor libraries that the root layout loads on every page. Each group gets
// its own chunk, so the entry chunk stays small and the browser can cache the
// libraries apart from the app code. Do not add libraries that only lazy
// routes use: a manual chunk loads with the entry.
const vendorChunks: Record<string, string[]> = {
  react: ['react', 'react-dom', 'scheduler'],
  router: ['@tanstack/react-router', '@tanstack/router-core', '@tanstack/history'],
  query: ['@tanstack/react-query', '@tanstack/query-core'],
  motion: ['motion', 'framer-motion', 'motion-dom', 'motion-utils'],
}

function vendorChunk(id: string): string | undefined {
  const match = id.match(/.*\/node_modules\/((?:@[^/]+\/)?[^/]+)\//)
  if (!match) return undefined
  return Object.keys(vendorChunks).find((chunk) =>
    vendorChunks[chunk].includes(match[1]),
  )
}

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
          include: ['src/**/*.test.ts', 'e2e/**/*.test.ts'],
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
            commands: { answerRequests, holdRequests, movePointerOnto, pressPointer, releasePointer, releaseRequests },
          },
        },
      },
    ],
  },
  server: {
    proxy: {
      // BFF management endpoints (login, logout, user)
      '/bff': {
        target: process.env.BFF_HTTPS || process.env.BFF_HTTP,
        changeOrigin: true,
        secure: false,
        configure: failStalledBffRequests
      },
      // OIDC callback endpoints
      '/signin-oidc': {
        target: process.env.BFF_HTTPS || process.env.BFF_HTTP,
        changeOrigin: true,
        secure: false,
        configure: failStalledBffRequests
      },
      '/signout-callback-oidc': {
        target: process.env.BFF_HTTPS || process.env.BFF_HTTP,
        changeOrigin: true,
        secure: false,
        configure: failStalledBffRequests
      },
      // API calls proxied through BFF
      '/api': {
        target: process.env.BFF_HTTPS || process.env.BFF_HTTP,
        changeOrigin: true,
        secure: false,
        configure: failStalledBffRequests
      }
    }
  }
})

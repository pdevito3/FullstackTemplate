/// <reference types="vitest/config" />
import path from 'path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import svgr from 'vite-plugin-svgr'
import { playwright } from '@vitest/browser-playwright'
import { TanStackRouterVite } from '@tanstack/router-plugin/vite'
import { holdRequests, releaseRequests } from './vitest.commands'

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
  // Playwright runs the e2e tests in e2e/.
  test: {
    projects: [
      {
        extends: true,
        test: {
          name: 'unit',
          include: ['src/**/*.test.ts'],
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
        // Vite finds the dependencies of the tests before the run. A dependency that Vite finds during
        // the run reloads the test page, and the test fails.
        optimizeDeps: {
          entries: ['src/**/*.test.tsx'],
        },
        test: {
          name: 'browser',
          include: ['src/**/*.test.tsx'],
          setupFiles: ['./src/test/browser-setup.ts'],
          browser: {
            enabled: true,
            provider: playwright(),
            headless: true,
            instances: [{ browser: 'chromium' }],
            commands: { holdRequests, releaseRequests },
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
        secure: false
      },
      // OIDC callback endpoints
      '/signin-oidc': {
        target: process.env.BFF_HTTPS || process.env.BFF_HTTP,
        changeOrigin: true,
        secure: false
      },
      '/signout-callback-oidc': {
        target: process.env.BFF_HTTPS || process.env.BFF_HTTP,
        changeOrigin: true,
        secure: false
      },
      // API calls proxied through BFF
      '/api': {
        target: process.env.BFF_HTTPS || process.env.BFF_HTTP,
        changeOrigin: true,
        secure: false
      }
    }
  }
})

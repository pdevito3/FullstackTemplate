import path from 'path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import svgr from 'vite-plugin-svgr'
import { TanStackRouterVite } from '@tanstack/router-plugin/vite'

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

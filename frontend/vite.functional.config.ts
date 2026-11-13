import path from 'path'
import { defineConfig, mergeConfig, type PluginOption } from 'vite'
import base from './vite.config'

/**
 * The build of the functional suite (playwright.config.ts): the production build of the app, plus two inputs that only
 * the tests open. It goes to dist-functional, so dist stays the build that ships.
 *
 * - The page of the shared components, at the same path as on the dev server.
 * - `test/notification.js`, which re-exports the toast queue from the chunk that the app loads, so a test that imports
 *   it gets the queue that the page shows.
 *
 * The inputs share chunks with the app, so the chunks differ from those of `pnpm build`. The first page budget counts
 * the chunks of each input, so this build leaves it out, and `pnpm build` checks it.
 */
const isBudget = (plugin: PluginOption) => typeof plugin === 'object' && plugin !== null && 'name' in plugin && plugin.name === 'first-page-budget'

export default mergeConfig(
  { ...base, plugins: base.plugins?.filter((plugin) => !isBudget(plugin)) },
  defineConfig({
    build: {
      outDir: 'dist-functional',
      rollupOptions: {
        // Vite drops the export names of each entry. A test imports Notification by its name.
        preserveEntrySignatures: 'exports-only',
        input: {
          index: path.resolve(__dirname, 'index.html'),
          'shared-components': path.resolve(__dirname, 'src/test/fixtures/shared-components.html'),
          notification: path.resolve(__dirname, 'src/components/notification.ts'),
        },
        output: {
          entryFileNames: (chunk) => (chunk.name === 'notification' ? 'test/notification.js' : 'assets/[name]-[hash].js'),
        },
      },
    },
  }),
)

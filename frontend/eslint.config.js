import js from '@eslint/js'
import globals from 'globals'
import importX from 'eslint-plugin-import-x'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  // Kubb writes src/api/generated (and one sibling folder for each other API version). Run `pnpm generate:api` to change it.
  { ignores: ['dist', 'src/api/generated*'] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
      'import-x': importX,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': [
        'error',
        { allowConstantExport: true },
      ],
      // docs/frontend/code-style.md: packages first, then `@/` imports, then relative imports.
      'import-x/order': [
        'error',
        {
          groups: ['builtin', 'external', 'internal', ['parent', 'sibling', 'index']],
          pathGroups: [
            { pattern: '@/**', group: 'internal', patternOptions: { dot: true } },
            { pattern: '@tests/**', group: 'internal' },
          ],
          pathGroupsExcludedImportTypes: [],
          'newlines-between': 'ignore',
        },
      ],
      // docs/frontend.md, section "Links": a link to a route takes typed `to` and `params`, and keeps the React Aria behavior.
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: 'react-aria-components',
              importNames: ['Link'],
              message: 'Use Link from @/components/ui/link. It takes typed `to` and `params`. The href of a React Aria Link is a string that TypeScript does not check.',
            },
            {
              name: '@animate-ui/react/primitives/react-aria',
              importNames: ['Link'],
              message: 'Use Link from @/components/ui/link. It takes typed `to` and `params`. The href of a React Aria Link is a string that TypeScript does not check.',
            },
            {
              name: '@tanstack/react-router',
              importNames: ['Link'],
              message: 'Use Link from @/components/ui/link. It has the React Aria focus and press behavior.',
            },
          ],
        },
      ],
    },
  },
  {
    // The router link builds on the React Aria Link.
    files: ['src/components/ui/link.tsx'],
    rules: { 'no-restricted-imports': 'off' },
  },
)

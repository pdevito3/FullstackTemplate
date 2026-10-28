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
  {
    // docs/frontend/code-style.md, section "Dates and times": the SPA shows and reads a time in the zone of its place,
    // through src/lib/zoned-time.ts. These APIs use the zone of the browser. A test sets its clock from local parts.
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/lib/zoned-time.ts', 'src/**/*.test.{ts,tsx}'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'CallExpression[callee.property.name=/^toLocale(Date|Time)?String$/]',
          message: 'This uses the zone of the browser. Use formatInstant (a time of a place) or formatDate (a date with no time) from @/lib/zoned-time. For a number, use Intl.NumberFormat.',
        },
        {
          selector: "MemberExpression[object.name='Intl'][property.name='DateTimeFormat']",
          message: 'Use formatInstant (a time of a place) or formatDate (a date with no time) from @/lib/zoned-time. They set the zone.',
        },
        {
          selector: 'CallExpression[arguments.length=0][callee.property.name=/^get(FullYear|Month|Date|Day|Hours|Minutes|Seconds|TimezoneOffset)$/]',
          message: 'This reads the date or the time in the zone of the browser. Use wallClockAt(instant, zone) or todayAt(zone, now) from @/lib/zoned-time.',
        },
        {
          selector: 'CallExpression[callee.property.name=/^set(FullYear|Month|Date|Hours|Minutes|Seconds)$/]',
          message: 'This sets the date or the time in the zone of the browser. Use instantAt(wallClock, zone) from @/lib/zoned-time.',
        },
        {
          selector: "NewExpression[callee.name='Date'][arguments.length>1]",
          message: 'This makes a time in the zone of the browser. Use new CalendarDate(...) for a date, or instantAt(wallClock, zone) from @/lib/zoned-time for a time of a place.',
        },
        {
          selector: "ImportDeclaration[source.value='@internationalized/date'] > ImportSpecifier[imported.name=/^(getLocalTimeZone|today|now|DateFormatter)$/]",
          message: 'Use todayAt(zone, now) and browserTimeZone() from @/lib/zoned-time, and its formatters. Take the zone of the place, not of the browser.',
        },
        {
          selector: "ImportDeclaration[source.value=/^(react-aria|@react-aria\\u002Fi18n)(\\u002F|$)/] > ImportSpecifier[imported.name=/^(useDateFormatter|DateFormatter)$/]",
          message: 'The React Aria date formatter formats in the zone of the browser unless it is given one. Use formatInstant (a time of a place) or formatDate (a date with no time) from @/lib/zoned-time.',
        },
        {
          selector: 'ImportDeclaration[source.value=/^date-fns/]',
          message: 'date-fns formats in the zone of the browser. Use @/lib/zoned-time.',
        },
      ],
    },
  },
)

import js from '@eslint/js'
import globals from 'globals'
import importX from 'eslint-plugin-import-x'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import ts from 'typescript'

// docs/frontend.md, section "Links": a link to a route takes typed `to` and `params`, and keeps the React Aria behavior.
const linkImports = [
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
]

// The actions of a Playwright Page or Frame that take a selector string first. A Locator has the same actions with no selector.
const selectorActions = 'click|dblclick|fill|type|press|pressSequentially|check|uncheck|setChecked|hover|tap|focus|selectOption|setInputFiles|dispatchEvent|textContent|innerText|innerHTML|getAttribute|inputValue|isChecked|isDisabled|isEditable|isEnabled|isHidden|isVisible|waitForSelector|dragAndDrop'
const stringSelectorMessage = 'Do not give a selector string to an action. Find the element with getByRole or getByLabel in the *.locators.ts file of the feature, and call the action on that Locator.'
// e2e/ has no features. The selectors of the login page of an IdP are in its file in e2e/idps/.
const stackSelectorMessage = 'Do not give a selector string to an action. Put a selector of the login page of an IdP in its e2e/idps/*.json file and call the action on page.locator(idp.<name>Selector). For the app or Swagger UI, find the element with getByRole or getByLabel and call the action on that Locator.'

const isPlaywright = (type, names) => {
  const symbol = type.getSymbol()
  return names.includes(symbol?.getName())
    && symbol.declarations?.some((declaration) => declaration.getSourceFile().fileName.includes('/playwright-core/'))
}

// An ElementHandle has the other actions with no selector, and its waitForSelector takes one.
const takesSelector = (type, action) =>
  isPlaywright(type, ['Page', 'Frame']) || (action === 'waitForSelector' && isPlaywright(type, ['ElementHandle']))

const noPageActionRule = {
  meta: {
    type: 'problem',
    schema: [{ type: 'string' }],
    messages: {
      selector: '{{message}}',
      any: 'This value has the type any, so the lint cannot check this call. Give the value its type, such as Page or Locator.',
    },
  },
  create(context) {
    const services = context.sourceCode.parserServices
    return {
      [`CallExpression > MemberExpression.callee[property.name=/^(${selectorActions})$/]`](callee) {
        const type = services.getTypeAtLocation(callee.object)
        if (type.flags & ts.TypeFlags.Any)
          context.report({ node: callee.parent, messageId: 'any' })
        else if ((type.isUnion() ? type.types : [type]).some((part) => takesSelector(part, callee.property.name)))
          context.report({ node: callee.parent, messageId: 'selector', data: { message: context.options[0] } })
      },
    }
  },
}

const selectorStringRule = (message) => ({
  selector: 'CallExpression[callee.property.name=/^\\$\\$?(eval)?$/]',
  message,
})

const testOnlyRule = {
  selector: "MemberExpression[property.name='only']:matches([object.name='test'], [object.object.name='test'])",
  message: 'Remove test.only. CI runs each test.',
}

const externalPageFileRules = [selectorStringRule(stackSelectorMessage), testOnlyRule]

// docs/frontend/testing.md, section "Locators, tasks, and specs". Each message names the fix.
const testFileRules = [
  selectorStringRule(stringSelectorMessage),
  {
    selector: "CallExpression[callee.property.name='nth'], CallExpression[arguments.length=0][callee.property.name=/^(first|last)$/]",
    message: 'Do not find an element by its position. Give the element a role and a name in the app, and find it by them in the *.locators.ts file of the feature.',
  },
  {
    selector: "CallExpression[callee.property.name='waitForTimeout']",
    message: 'Do not wait for a time. In the *.tasks.ts file of the feature, wait for the response of the request, or let an expect retry until the state shows.',
  },
]

const specFileRules = [
  {
    selector: 'CallExpression[callee.property.name=/^(locator|frameLocator|getBy(Role|Text|Label|Placeholder|AltText|Title|TestId))$/]',
    message: 'A spec does not know a selector. Move the selector to the *.locators.ts file of the feature, and read it through the page object of the feature.',
  },
  {
    selector: "CallExpression[callee.object.name='page'][callee.property.name=/^waitFor/]",
    message: 'A spec does not know a wait. Move the wait to a task in the *.tasks.ts file of the feature, and arm it before the action.',
  },
  {
    selector: "CallExpression[callee.name='setTimeout']",
    message: 'A spec does not wait for a time. Wait for a response or a state in a task in the *.tasks.ts file of the feature.',
  },
  testOnlyRule,
]

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
      'no-restricted-imports': [
        'error',
        {
          paths: linkImports,
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
    files: ['tests/**/*.ts', 'e2e/**/*.ts'],
    languageOptions: {
      parserOptions: { project: './tsconfig.tests.json', tsconfigRootDir: import.meta.dirname },
    },
    plugins: { playwright: { rules: { 'no-page-action': noPageActionRule } } },
  },
  {
    files: ['tests/**/*.ts'],
    rules: {
      'playwright/no-page-action': ['error', stringSelectorMessage],
      'no-restricted-syntax': ['error', ...testFileRules],
    },
  },
  {
    files: ['e2e/**/*.ts'],
    rules: {
      'playwright/no-page-action': ['error', stackSelectorMessage],
      'no-restricted-syntax': ['error', ...externalPageFileRules],
    },
  },
  {
    files: ['tests/**/*.spec.ts'],
    rules: {
      'no-restricted-syntax': ['error', ...testFileRules, ...specFileRules],
      'no-restricted-imports': [
        'error',
        {
          paths: [
            ...linkImports,
            {
              name: '@playwright/test',
              importNames: ['test', 'expect'],
              message: 'Import test and expect from @tests/support/fixtures. Its test fails on a request that no test mocked.',
            },
          ],
        },
      ],
    },
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

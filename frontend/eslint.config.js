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
export const stringSelectorMessage = 'Do not give a selector string to an action. Find the element with getByRole or getByLabel in the *.locators.ts file of the feature, and call the action on that Locator.'
const stackSelectorMessage = 'Do not give a selector string to an action. Put a selector of the login page of an IdP in its e2e/idps/*.json file and call the action on page.locator(idp.<name>Selector). For the app or Swagger UI, find the element with getByRole or getByLabel and call the action on that Locator.'

const isPlaywright = (type, names) => {
  const symbol = type.getSymbol()
  return names.includes(symbol?.getName())
    && symbol.declarations?.some((declaration) => declaration.getSourceFile().fileName.includes('/playwright-core/'))
}

// An ElementHandle has the other actions with no selector, and its waitForSelector takes one.
const takesSelector = (type, action) =>
  isPlaywright(type, ['Page', 'Frame']) || (action === 'waitForSelector' && isPlaywright(type, ['ElementHandle']))

export const noPageActionRule = {
  meta: {
    type: 'problem',
    schema: [{ type: 'string' }],
    defaultOptions: [stringSelectorMessage],
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

const within = (inner, outer) => inner.range[0] >= outer.range[0] && inner.range[1] <= outer.range[1]

const referencesIn = (sourceCode, node) => {
  const found = []
  const visit = (scope) => {
    found.push(...scope.references.filter((ref) => !ref.isTypeReference && within(ref.identifier, node)))
    scope.childScopes.forEach(visit)
  }
  visit(sourceCode.getScope(node))
  return found
}

const resolve = (scope, name) => {
  for (let at = scope; at; at = at.upper) {
    const variable = at.set.get(name)
    if (variable)
      return variable
  }
  return null
}

const isLocal = (variable) => variable && !['module', 'global'].includes(variable.scope.type)

const initOf = (variable) => {
  const def = variable.defs[0]
  return def?.type === 'Variable' ? def.node.init : null
}

const callWrappers = ['MemberExpression', 'ChainExpression', 'TSNonNullExpression', 'AwaitExpression']

const hookCalledBy = (init) => {
  let node = init
  while (node && callWrappers.includes(node.type))
    node = node.object ?? node.expression ?? node.argument
  return node?.type === 'CallExpression' && node.callee.type === 'Identifier' && /^use[A-Z]/.test(node.callee.name)
    ? node.callee.name
    : null
}

const fieldOfCall = (init) => {
  let node = init
  let field = null
  while (node && callWrappers.includes(node.type)) {
    if (node.type === 'MemberExpression')
      field = node.computed ? null : node.property.name
    node = node.object ?? node.expression ?? node.argument
  }
  return field
}

const isFirstValueName = (name) => /^(initial|default)([A-Z]|$)/.test(name)
const ownStateHooks = new Set(['useState', 'useReducer', 'useRef', 'useId'])

const copiedRoots = (node) => {
  switch (node?.type) {
    case 'Identifier':
      return [node]
    case 'MemberExpression':
      return copiedRoots(node.object)
    case 'ChainExpression':
    case 'TSNonNullExpression':
    case 'TSAsExpression':
    case 'TSSatisfiesExpression':
      return copiedRoots(node.expression)
    case 'LogicalExpression':
      return [...copiedRoots(node.left), ...copiedRoots(node.right)]
    case 'ConditionalExpression':
      return [...copiedRoots(node.consequent), ...copiedRoots(node.alternate)]
    case 'ArrowFunctionExpression':
      return node.body.type === 'BlockStatement' ? [] : copiedRoots(node.body)
    default:
      return []
  }
}

const firstValueAdvice = 'If {{source}} is only the first value and the user owns the state from then on, name it initialX or defaultX.'
const timerCalls = new Set(['setTimeout', 'setInterval', 'requestAnimationFrame'])

const functionNameOf = (fn) => fn.id?.name ?? (fn.parent.type === 'VariableDeclarator' ? fn.parent.id.name : null)

// A hook result is server data only through its `data`, so `const { data } = useRecord(id)` is a copy source and
// `useIsMobile()` or `query.isSuccess` is not.
const takesQueryData = (variable, property) => {
  const { id } = variable.defs[0].node
  if (id.type === 'Identifier')
    return (fieldOfCall(initOf(variable)) ?? property) === 'data'
  const field = id.type === 'ObjectPattern' && id.properties.find((p) => p.type === 'Property'
    && (p.value.type === 'AssignmentPattern' ? p.value.left : p.value) === variable.identifiers[0])
  return Boolean(field) && !field.computed && field.key.name === 'data'
}

export const noCopiedStateRule = {
  meta: {
    type: 'problem',
    schema: [],
    messages: {
      prop: `This state copies {{source}}, so it keeps the old value after {{source}} changes. Read the value where you use it (docs/frontend/server-state.md, rule 2). Give server data to a form in defaultValues. ${firstValueAdvice.replace('name it', 'name the prop')}`,
      argument: `This state copies {{source}}, so it keeps the old value after {{source}} changes. Read the value where you use it (docs/frontend/server-state.md, rule 2). ${firstValueAdvice.replace('name it', 'name the argument')}`,
      query: 'This state copies {{source}}, so it keeps the old value after the query reads the record again. Read the value where you use it (docs/frontend/server-state.md, rule 2). Give server data to a form in defaultValues.',
    },
  },
  create(context) {
    const { sourceCode } = context
    const sourceOf = (identifier, seen) => {
      const variable = resolve(sourceCode.getScope(identifier), identifier.name)
      if (!isLocal(variable) || seen.has(variable))
        return null
      seen.add(variable)
      const { parent } = identifier
      const property = parent.type === 'MemberExpression' && parent.object === identifier && !parent.computed ? parent.property.name : null
      const isParameter = variable.defs[0]?.type === 'Parameter'
      const init = initOf(variable)
      if ((isParameter && isFirstValueName(variable.name)) || (property && isFirstValueName(property)))
        return null
      if (isParameter)
        return /^use[A-Z]/.test(functionNameOf(variable.scope.block) ?? '')
          ? { kind: 'argument', label: `the argument \`${variable.name}\`` }
          : { kind: 'prop', label: `the prop \`${variable.name}\`` }
      const hook = init && hookCalledBy(init)
      if (hook)
        return ownStateHooks.has(hook) || !takesQueryData(variable, property) ? null : { kind: 'query', label: `the result of \`${hook}\`` }
      return copiedRoots(init).map((root) => sourceOf(root, seen)).find(Boolean) ?? null
    }

    const functionOf = (node) => {
      let parent = node.parent
      while (parent && !['FunctionDeclaration', 'FunctionExpression', 'ArrowFunctionExpression'].includes(parent.type))
        parent = parent.parent
      return parent
    }
    const isTimerCallback = (fn) => fn?.parent.type === 'CallExpression' && fn.parent.arguments.includes(fn)
      && timerCalls.has(fn.parent.callee.name ?? fn.parent.callee.property?.name)
    const sourceOfValue = (node) => copiedRoots(node).map((root) => sourceOf(root, new Set())).find(Boolean)
    const readsSource = (node, source) => Boolean(node)
      && referencesIn(sourceCode, node).some((ref) => sourceOf(ref.identifier, new Set())?.label === source.label)
    // The render sets the state again from its source (client-state.md, "Effects"), or a timer sets it later, as a
    // debounce does. An effect that sets it at once is the copy that this rule reports.
    const setsAgain = (node, source) => {
      const setter = node.parent.type === 'VariableDeclarator' && node.parent.id.type === 'ArrayPattern' ? node.parent.id.elements[1] : null
      const variable = setter?.type === 'Identifier' && resolve(sourceCode.getScope(setter), setter.name)
      return Boolean(variable) && variable.references.some(({ identifier }) => {
        const call = identifier.parent
        const fn = call.type === 'CallExpression' && call.callee === identifier && functionOf(call)
        return Boolean(fn) && (fn === functionOf(node) || isTimerCallback(fn)) && readsSource(call.arguments[0], source)
      })
    }

    return {
      "CallExpression:matches([callee.name='useState'], [callee.property.name='useState'])"(node) {
        const source = sourceOfValue(node.arguments[0])
        if (source && !setsAgain(node, source))
          context.report({ node, messageId: source.kind, data: { source: source.label } })
      },
    }
  },
}

const keyedQueryCalls = 'useQuery|useSuspenseQuery|useInfiniteQuery|useSuspenseInfiniteQuery|queryOptions|infiniteQueryOptions|fetchQuery|prefetchQuery|ensureQueryData|fetchInfiniteQuery|prefetchInfiniteQuery'

const isInClientOptions = (identifier, queryFn) => {
  for (let node = identifier.parent; node && node !== queryFn; node = node.parent)
    if (node.type === 'Property' && !node.computed && node.key.name === 'options')
      return true
  return false
}

const isConstant = (node) => node?.type === 'Literal' || (node?.type === 'TemplateLiteral' && node.expressions.length === 0)
  || (node?.type === 'UnaryExpression' && isConstant(node.argument))

const isValue = (variable) => {
  const def = variable.defs[0]
  if (!def || ['FunctionName', 'ImportBinding', 'ClassName'].includes(def.type))
    return false
  const init = initOf(variable)
  if (def.parent?.kind === 'const' && def.node.id.type === 'Identifier' && isConstant(init))
    return false
  return !(init && (['ArrowFunctionExpression', 'FunctionExpression'].includes(init.type) || hookCalledBy(init) === 'useQueryClient'))
}

const fieldsRead = (identifier) => {
  const fields = []
  for (let node = identifier; ;) {
    const { parent } = node
    if (['TSNonNullExpression', 'ChainExpression'].includes(parent.type)) {
      node = parent
    } else if (parent.type === 'MemberExpression' && parent.object === node && !parent.computed
      && !(parent.parent.type === 'CallExpression' && parent.parent.callee === parent)) {
      fields.push(parent.property.name)
      node = parent
    } else {
      return fields
    }
  }
}

const readOf = (ref) => ({ variable: ref.resolved, fields: fieldsRead(ref.identifier) })
const covers = (key, read) => key.variable === read.variable && key.fields.every((field, i) => read.fields[i] === field)

export const queryKeyHasInputsRule = {
  meta: {
    type: 'problem',
    schema: [],
    messages: {
      missing: 'The query function reads `{{name}}`, but the query key does not have it, so two calls with another `{{name}}` share one cache entry. Put `{{name}}` in the key (docs/frontend/server-state.md, rule 3).',
    },
  },
  create(context) {
    const { sourceCode } = context
    const keyReads = (node) => {
      const reads = []
      const followed = new Set()
      const add = (ref) => {
        const variable = ref.resolved
        if (!isLocal(variable))
          return
        reads.push(readOf(ref))
        if (followed.has(variable))
          return
        followed.add(variable)
        const init = initOf(variable)
        if (init)
          referencesIn(sourceCode, init).forEach(add)
      }
      referencesIn(sourceCode, node).forEach(add)
      return reads
    }

    return {
      [`CallExpression:matches([callee.name=/^(${keyedQueryCalls})$/], [callee.property.name=/^(${keyedQueryCalls})$/]) > ObjectExpression.arguments:first-child`](options) {
        const property = (name) => options.properties.find((p) => p.type === 'Property' && !p.computed && p.key.name === name)?.value
        const queryKey = property('queryKey')
        const queryFn = property('queryFn')
        if (!queryKey || !queryFn || !['ArrowFunctionExpression', 'FunctionExpression'].includes(queryFn.type))
          return
        const inKey = keyReads(queryKey)
        const isInKey = (read) => inKey.some((key) => covers(key, read))
        const comesFromKey = (variable, path = []) => {
          const init = initOf(variable)
          if (!init || !['useMemo', 'useCallback', null].includes(hookCalledBy(init)) || path.includes(variable))
            return false
          const reads = referencesIn(sourceCode, init).filter((ref) => isLocal(ref.resolved) && isValue(ref.resolved)).map(readOf)
          return reads.length > 0 && reads.every((read) => isInKey(read) || comesFromKey(read.variable, [...path, variable]))
        }
        const reported = new Set()
        for (const ref of referencesIn(sourceCode, queryFn)) {
          const variable = ref.resolved
          if (!isLocal(variable) || within(variable.scope.block, queryFn) || !isValue(variable) || isInClientOptions(ref.identifier, queryFn))
            continue
          const read = readOf(ref)
          const name = [variable.name, ...read.fields].join('.')
          if (isInKey(read) || comesFromKey(variable) || reported.has(name))
            continue
          reported.add(name)
          context.report({ node: ref.identifier, messageId: 'missing', data: { name } })
        }
      },
    }
  },
}

export const keyHasEachParamRule = {
  meta: {
    type: 'problem',
    schema: [],
    messages: {
      whole: 'Put the parameter `{{name}}` in the key whole, as an item of the array that the key factory returns, such as `[..., {{name}}]`. Do not take it apart, read a field of it, copy it, spread it, or give it to a call. A key that has only a part of it can leave out a value of the request, so two pages or two dates share one cache entry (docs/frontend/server-state.md, rule 3).',
      pattern: 'Take this parameter whole, give it a name, and put it in the key as an item of the array that the key factory returns, such as `(params: ListParams) => [..., params]`. A pattern, a default, or a rest element can leave out a value of the request, so two pages or two dates share one cache entry (docs/frontend/server-state.md, rule 3).',
    },
  },
  create(context) {
    const { sourceCode } = context
    const unwrap = (node) => ['TSAsExpression', 'TSSatisfiesExpression'].includes(node.parent.type) ? unwrap(node.parent) : node
    const ownerOf = (node) => node.type.endsWith('FunctionExpression') || node.type === 'FunctionDeclaration' ? node : ownerOf(node.parent)
    const isItemOfKey = (identifier, fn) => {
      if (identifier.parent.type !== 'ArrayExpression')
        return false
      const key = unwrap(identifier.parent)
      return (key.parent === fn && fn.body === key)
        || (key.parent.type === 'ReturnStatement' && ownerOf(key.parent) === fn)
    }
    const check = (fn) => {
      for (const param of fn.params) {
        if (param.type !== 'Identifier') {
          context.report({ node: param, messageId: 'pattern' })
          continue
        }
        const variable = sourceCode.getDeclaredVariables(fn).find((v) => v.identifiers.includes(param))
        if (variable.references.length === 0 || !variable.references.every(({ identifier }) => isItemOfKey(identifier, fn)))
          context.report({ node: param, messageId: 'whole', data: { name: param.name } })
      }
    }
    return { ':function': check }
  },
}

export const serverStatePlugin = {
  rules: { 'no-copied-state': noCopiedStateRule, 'query-key-has-inputs': queryKeyHasInputsRule, 'key-has-each-param': keyHasEachParamRule },
}

const selectorStringRule = (message) => ({
  selector: 'CallExpression[callee.property.name=/^\\$\\$?(eval)?$/]',
  message,
})

// docs/messages.md, rule 1: words made from a code, such as "Pago Pago" from Pacific/Pago_Pago, are not the words of the screen.
const codeToWordsMessage = 'Do not make words from a code (docs/messages.md, rule 1). Name a time zone with timeZoneLabel(zone) from @/lib/zoned-time. Give another code a property or a map with its words.'
export const codeToWordsRules = [
  {
    selector: "CallExpression[callee.property.name=/^replace(All)?$/]:matches([arguments.0.value='_'], [arguments.0.regex.pattern=/_/]):matches([arguments.1.value=' '], [arguments.1.quasis.0.value.cooked=' '][arguments.1.expressions.length=0])",
    message: codeToWordsMessage,
  },
  {
    selector: "CallExpression[callee.property.name='join'][arguments.0.value=' '][callee.object.callee.property.name='split'][callee.object.arguments.0.value='_']",
    message: codeToWordsMessage,
  },
]

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
    files: ['src/**/*.{ts,tsx}'],
    plugins: { 'server-state': serverStatePlugin },
    rules: {
      'server-state/no-copied-state': 'error',
      'server-state/query-key-has-inputs': 'error',
    },
  },
  {
    files: ['src/**/*.keys.ts'],
    rules: { 'server-state/key-has-each-param': 'error' },
  },
  {
    // docs/frontend.md, section "Bundle size": the route code splitter keeps each import that the route options do not use
    // as an inline type import, such as `import { type Option } from '...'`. TypeScript keeps that line as an import with
    // side effects, so the module and each module that it imports load with the entry chunk.
    files: ['src/routes/**/*.{ts,tsx}'],
    rules: { 'import-x/consistent-type-specifier-style': ['error', 'prefer-top-level'] },
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
        ...codeToWordsRules,
      ],
    },
  },
  {
    // The zone helpers may use the zone of the browser, but they name a zone in words too.
    files: ['src/lib/zoned-time.ts'],
    rules: { 'no-restricted-syntax': ['error', ...codeToWordsRules] },
  },
)

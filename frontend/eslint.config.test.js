import { ESLint, Linter } from 'eslint'
import tseslint from 'typescript-eslint'
import { describe, expect, it } from 'vitest'
import { antiSlopRules, armWaitFirstRule, codeToWordsRules, lateWaitMessage, noPageActionRule, serverStatePlugin, stringSelectorMessage } from './eslint.config.js'

const pageAction = "import type { Page } from '@playwright/test'\nexport const save = (page: Page) => page.click('#save')\n"

async function messagesOf(ruleEntry) {
  const eslint = new ESLint({
    cwd: import.meta.dirname,
    overrideConfigFile: true,
    overrideConfig: {
      files: ['tests/**/*.ts'],
      languageOptions: {
        parser: tseslint.parser,
        // With CI=true, typescript-eslint reads the types from the files on disk, not from the text that this test lints
        parserOptions: {
          project: './tsconfig.tests.json',
          tsconfigRootDir: import.meta.dirname,
          disallowAutomaticSingleRunInference: true,
        },
      },
      plugins: { playwright: { rules: { 'no-page-action': noPageActionRule } } },
      rules: { 'playwright/no-page-action': ruleEntry },
    },
  })
  // The parser needs a file of the tests program. ESLint lints the text, not the file on disk.
  const [result] = await eslint.lintText(pageAction, { filePath: 'tests/support/fixtures.ts' })
  return result.messages.map((message) => message.message)
}

// The first lint builds the TypeScript program of the tests, which takes more than the 5 s default on a cold machine
describe('playwright/no-page-action', { timeout: 60_000 }, () => {
  it('reports a selector string with the message of its option', async () => {
    expect(await messagesOf(['error', 'Use a Locator.'])).toEqual(['Use a Locator.'])
  })

  it('reports a selector string with the locator message when the rule has no option', async () => {
    expect(await messagesOf('error')).toEqual([stringSelectorMessage])
  })
})

async function lateWaitLines(body) {
  const eslint = new ESLint({
    cwd: import.meta.dirname,
    overrideConfigFile: true,
    overrideConfig: {
      files: ['tests/**/*.ts'],
      languageOptions: {
        parser: tseslint.parser,
        parserOptions: { project: './tsconfig.tests.json', tsconfigRootDir: import.meta.dirname, disallowAutomaticSingleRunInference: true },
      },
      plugins: { playwright: { rules: { 'arm-wait-first': armWaitFirstRule } } },
      rules: { 'playwright/arm-wait-first': 'error' },
    },
  })
  const code = `import type { Page, Request } from '@playwright/test'
export async function task(page: Page) {
  const answered = (path: string) => page.waitForResponse((r) => new URL(r.url()).pathname === path)
  const follow = async (): Promise<Request> => {
    const sent = page.waitForRequest('**/next')
    await page.click('#next')
    return sent
  }
${body}
}
`
  const [result] = await eslint.lintText(code, { filePath: 'tests/support/fixtures.ts' })
  expect(result.messages.every((message) => message.message === lateWaitMessage)).toBe(true)
  return result.messages.map((message) => message.line - 8)
}

describe('playwright/arm-wait-first', { timeout: 60_000 }, () => {
  it('reports a wait that starts after the action', async () => {
    const lines = await lateWaitLines(`  await page.reload()
  await page.waitForResponse('**/api')
  await answered('/api')
  await page.waitForEvent('load')
  await Promise.all([answered('/a'), answered('/b')])`)

    expect(lines).toEqual([2, 3, 4, 5])
  })

  it('reports a wait that the next statement awaits, with no action between', async () => {
    const lines = await lateWaitLines(`  const answer = answered('/api')
  await answer
  const loaded = page.waitForEvent('load')
  const page2 = await loaded`)

    expect(lines).toEqual([1, 3])
  })

  it('takes a wait armed before the action, a wait in a Promise.all with the action, an async action, and a goto', async () => {
    const lines = await lateWaitLines(`  const answer = answered('/api')
  await page.reload()
  await answer
  await Promise.all([answered('/api'), page.reload()])
  await follow()
  await page.goto('/')`)

    expect(lines).toEqual([])
  })
})

function serverStateMessages(code, file = 'src/domain/records/record.tsx') {
  const linter = new Linter({ configType: 'flat' })
  const rules = Object.keys(serverStatePlugin.rules).map((name) => [`server-state/${name}`, 'error'])
  return linter
    .verify(code, [{
      files: ['**/*.ts', '**/*.tsx'],
      languageOptions: { parser: tseslint.parser },
      plugins: { 'server-state': serverStatePlugin },
      rules: Object.fromEntries(file.endsWith('.keys.ts') ? rules : rules.filter(([name]) => !name.endsWith('key-has-each-param'))),
    }], file)
    .map((message) => `${message.ruleId?.replace('server-state/', '')}: ${message.message.split(',')[0]}`)
}

describe('server-state/no-copied-state', () => {
  it('reports state that copies a prop', () => {
    expect(serverStateMessages('function Name(props) { const [name] = useState(props.record.name) }'))
      .toEqual(['no-copied-state: This state copies the prop `props`'])
    expect(serverStateMessages("function Name({ record }) { const [name] = useState(record?.name ?? '') }"))
      .toEqual(['no-copied-state: This state copies the prop `record`'])
  })

  it('reports state that copies a query result, also through a local const', () => {
    const code = 'function Name({ id }) { const { data } = useRecord(id); const record = data!; const [name] = useState(record.name) }'

    expect(serverStateMessages(code)).toEqual(['no-copied-state: This state copies the result of `useRecord`'])
  })

  it('takes a prop that names a first value, a calculated first value, and the value of other state', () => {
    const code = `function Name({ defaultOpen, initialText, props, value }) {
      const [open] = useState(defaultOpen)
      const [text] = useState(props.initialText ?? initialText)
      const [date] = useState(() => parseDate(value))
      const [count] = useState(0)
      const [shown] = useState(count)
    }`

    expect(serverStateMessages(code)).toEqual([])
  })

  it('takes state that the render sets again from its source, and reports state that an effect sets again', () => {
    const synced = `function Search({ search }) {
      const [shown, setShown] = useState(search)
      if (search !== shown) setShown(search)
    }`
    const inEffect = `function Search({ search }) {
      const [shown, setShown] = useState(search)
      useEffect(() => { setShown(search) }, [search])
    }`

    expect(serverStateMessages(synced)).toEqual([])
    expect(serverStateMessages(inEffect)).toEqual(['no-copied-state: This state copies the prop `search`'])
  })

  it('takes a first-value name only from a prop, not from a local copy of a query result', () => {
    const code = "function Name({ id }) { const { data } = useRecord(id); const initialName = data?.name ?? ''; const [name] = useState(initialName) }"

    expect(serverStateMessages(code)).toEqual(['no-copied-state: This state copies the result of `useRecord`'])
  })

  it.each([
    ['function A1(props) { const [name] = useState(props.record.name) }', 'the prop `props`'],
    ['function A2({ name }) { const [n] = useState(name) }', 'the prop `name`'],
    ["function A3({ id }) { const { data } = useRoomQ(id); const [n] = useState(data?.name ?? '') }", 'the result of `useRoomQ`'],
    ['function A4({ name }) { const copy = name; const [n] = useState(() => copy) }', 'the prop `name`'],
    ['function A5({ name }) { const [n, setN] = useState(name); useEffect(() => setN(name), [name]) }', 'the prop `name`'],
    ['function useCopy(value) { const [copy] = useState(value); return copy }', 'the argument `value`'],
    ['function A6({ id }) { const query = useRoomQ(id); const [n] = useState(query.data?.name) }', 'the result of `useRoomQ`'],
    ['function A7({ id }) { const { data: initialRecord } = useRoomQ(id); const [r] = useState(initialRecord) }', 'the result of `useRoomQ`'],
    ['function A8({ id }) { const initialRecord = useRoomQ(id).data; const [r] = useState(initialRecord) }', 'the result of `useRoomQ`'],
  ])('reports the probe %s', (code, source) => {
    expect(serverStateMessages(code)).toEqual([`no-copied-state: This state copies ${source}`])
  })

  it.each([
    'function B1({ first, last }) { const full = `${first} ${last}`; return full }',
    'function B3({ items }) { const [prevItems, setPrevItems] = useState(items); const [selection, setSelection] = useState(null); if (items !== prevItems) { setPrevItems(items); setSelection(null) } }',
    'function B4({ value }) { const [text, setText] = useState(value); const [prev, setPrev] = useState(value); if (value !== prev) { setPrev(value); setText(value) } }',
    'function B5({ initialCount }) { const [c] = useState(initialCount) }',
    'function B6(props) { const [o] = useState(props.defaultOpen ?? false) }',
    `function useDebouncedValue(value, delay = 300) {
      const [debounced, setDebounced] = useState(value)
      useEffect(() => {
        const timer = setTimeout(() => setDebounced(value), delay)
        return () => clearTimeout(timer)
      }, [value, delay])
      return debounced
    }`,
    'function B7() { const isMobile = useIsMobile(); const [open] = useState(isMobile) }',
    "function B8({ id }) { const q = useQuery({ queryKey: ['x', id], queryFn: () => id }); const [ready] = useState(q.isSuccess) }",
    'function B9({ value }) { const [text, setText] = useState(value); const [prev, setPrev] = useState(value); if (value !== prev) { setPrev(value); setText(value.trim()) } }',
  ])('takes the probe %s', (code) => {
    expect(serverStateMessages(code)).toEqual([])
  })

  it('names the prop only for a prop, and gives the rename advice only for a prop', () => {
    const linter = new Linter({ configType: 'flat' })
    const messages = (code) => linter.verify(code, [{
      languageOptions: { parser: tseslint.parser },
      plugins: { 'server-state': serverStatePlugin },
      rules: { 'server-state/no-copied-state': 'error' },
    }]).map((message) => message.message)

    expect(messages("function Name({ id }) { const { data } = useRecord(id); const [name] = useState(data?.name ?? '') }")[0]).not.toMatch(/prop|initialX/)
    expect(messages('function useCopy(value) { const [copy] = useState(value) }')[0]).not.toMatch(/prop/)
    expect(messages('function Name({ name }) { const [n] = useState(name) }')[0]).toMatch(/name the prop initialX or defaultX/)
  })
})

describe('server-state/query-key-has-inputs', () => {
  it('reports a value that the query function reads and the key does not have', () => {
    const code = `function useRecords(search, page) {
      return useQuery({ queryKey: RecordKeys.list({ search }), queryFn: ({ signal }) => getRecordList({ query: { search, pageNumber: page }, signal }) })
    }`

    expect(serverStateMessages(code)).toEqual(['query-key-has-inputs: The query function reads `page`'])
  })

  it('takes a key with each value, a key from a local const, client options, and module constants', () => {
    const code = `const PAGE_SIZE = 10
    function useRecord(id, params, suppressErrorToast) {
      const key = RecordKeys.list(params)
      const detail = queryOptions({ queryKey: RecordKeys.detail(id ?? ''), queryFn: ({ signal }) => getRecord({ path: { id: id! }, signal, options: { suppressErrorToast } }) })
      const list = useQuery({ queryKey: key, queryFn: async ({ signal }) => getRecordList({ query: { search: params.search, pageSize: PAGE_SIZE }, signal }) })
      return { detail, list }
    }`

    expect(serverStateMessages(code)).toEqual([])
  })

  it('takes a local that the query function reads when the key has each value it comes from', () => {
    const code = `function useRecords(params) {
      const search = params.search.trim()
      return useQuery({ queryKey: RecordKeys.list(params), queryFn: () => getRecordList({ query: { search, pageNumber: params.page } }) })
    }`

    expect(serverStateMessages(code)).toEqual([])
  })

  it('reports a local that a hook makes from a value in the key', () => {
    const code = `function useRecords(search) {
      const debounced = useDebouncedValue(search)
      return useQuery({ queryKey: RecordKeys.list({ search }), queryFn: () => getRecordList({ query: { search: debounced } }) })
    }`

    expect(serverStateMessages(code)).toEqual(['query-key-has-inputs: The query function reads `debounced`'])
  })

  it('takes a useMemo or useCallback local that comes only from values in the key', () => {
    const code = `function useRecords(search, id) {
      const filter = useMemo(() => build(search), [search])
      const load = useCallback(() => getRecord(id), [id])
      return [
        useQuery({ queryKey: RecordKeys.list({ search }), queryFn: () => getRecordList({ query: { filter } }) }),
        useQuery({ queryKey: RecordKeys.detail(id), queryFn: () => load() }),
      ]
    }`

    expect(serverStateMessages(code)).toEqual([])
  })

  it('reports a local that refers to itself, and does not overflow the stack', () => {
    const code = `function useRecord(id) {
      const node = { self: () => node.id, id }
      return useQuery({ queryKey: RecordKeys.detail(id), queryFn: () => getRecord(node) })
    }`

    expect(serverStateMessages(code)).toEqual(['query-key-has-inputs: The query function reads `node`'])
  })

  it('reports a field that the query function reads when the key has another field of the same value', () => {
    const code = `function useRecords(params) {
      return useQuery({ queryKey: RecordKeys.list(params.search), queryFn: () => getRecordList({ query: { search: params.search, pageNumber: params.page } }) })
    }`

    expect(serverStateMessages(code)).toEqual(['query-key-has-inputs: The query function reads `params.page`'])
  })

  it('reports a whole value that the query function reads when the key has only a field of it', () => {
    const code = `function useRecords(params) {
      return useQuery({ queryKey: [...RecordKeys.lists(), { search: params.search }], queryFn: () => getRecordList({ query: { ...params } }) })
    }`

    expect(serverStateMessages(code)).toEqual(['query-key-has-inputs: The query function reads `params`'])
  })

  it('takes a field of a value when the key has the whole value or the same field', () => {
    const code = `function useRecords(params, filter) {
      return [
        useQuery({ queryKey: RecordKeys.list(params), queryFn: () => getRecordList({ query: { search: params.search.trim(), pageNumber: params.page } }) }),
        useQuery({ queryKey: ['records', filter.text, filter.page], queryFn: () => getRecordList({ query: { search: filter.text, pageNumber: filter.page } }) }),
      ]
    }`

    expect(serverStateMessages(code)).toEqual([])
  })

  it.each([
    ['useQuery({ queryKey: ["x", id], queryFn: () => get({ id, page }) })', ['page'], 'id, page'],
    ['useQuery({ queryKey: ["x", params], queryFn: () => get({ ...params, tenantId }) })', ['tenantId'], 'params, tenantId'],
    ['queryOptions({ queryKey: ["x", id], queryFn: () => get({ id, date }) })', ['date'], 'id, date'],
    ['useQuery({ queryKey: ["x", params], queryFn: ({ signal }) => get({ page: params.page, search: params.search, size: PAGE_SIZE, signal }) })', [], 'params'],
    ['useQuery({ queryKey: ["x", params], queryFn: () => get({ filter: toFilter(params.search) }) })', [], 'params'],
    ['useQuery({ queryKey: ["x", id], queryFn: () => get({ id, options: { suppressErrorToast: opts.suppress } }) })', [], 'id, opts'],
    ['useQuery({ queryKey: ["x", search.trim()], queryFn: () => get({ search: search.trim() }) })', [], 'search'],
    ['(() => { const pageSize = 25; const now = Date.now(); return useQuery({ queryKey: ["x", params], queryFn: () => get({ ...params, pageSize, now }) }) })()', ['now'], 'params'],
    ['useInfiniteQuery({ queryKey: ["x", search], queryFn: ({ pageParam, signal }) => get({ search, pageParam, signal }), initialPageParam: 1, getNextPageParam: () => 2 })', [], 'search'],
  ])('checks the probe %s', (call, names, parameters) => {
    const code = `const PAGE_SIZE = 10\nfunction useProbe(${parameters}) { return ${call} }`

    expect(serverStateMessages(code)).toEqual(names.map((name) => `query-key-has-inputs: The query function reads \`${name}\``))
  })
})

describe('server-state/key-has-each-param', () => {
  const keysFile = 'src/domain/records/apis/record.keys.ts'
  const keysOf = (property) => `export const RecordKeys = {\n  all: ['records'] as const,\n  lists: () => [...RecordKeys.all, 'list'] as const,\n  ${property}\n}`
  const whole = (name) => `key-has-each-param: Put the parameter \`${name}\` in the key whole`
  const pattern = 'key-has-each-param: Take this parameter whole'

  it.each([
    ['a pattern in the signature that drops a field', 'list: ({ search }: RecordListParams) => [...RecordKeys.lists(), { search }] as const,', [pattern]],
    ['a pattern in the signature that keeps each field', 'list: ({ page, search }: RecordListParams) => [...RecordKeys.lists(), { page, search }] as const,', [pattern]],
    ['a pattern with an inline type', 'list: ({ search, page }: { search: string; page: number }) => [...RecordKeys.lists(), { search, page }] as const,', [pattern]],
    ['a pattern with a rest element', 'list: ({ search, ...rest }: RecordListParams) => [...RecordKeys.lists(), search, rest] as const,', [pattern]],
    ['a default value', 'list: (page = 1) => [...RecordKeys.lists(), page] as const,', [pattern]],
    ['a destructure in the body', 'list: (params: RecordListParams) => { const { search } = params; return [...RecordKeys.lists(), { search }] as const },', [whole('params')]],
    ['an alias in the body', 'list: (params: RecordListParams) => { const p = params; return [...RecordKeys.lists(), p.search] as const },', [whole('params')]],
    ['a field read', 'list: (params: RecordListParams) => [...RecordKeys.lists(), { search: params.search }] as const,', [whole('params')]],
    ['each field read', 'list: (params: RecordListParams) => [...RecordKeys.lists(), { page: params.page, search: params.search }] as const,', [whole('params')]],
    ['one field read twice', 'list: (params: RecordListParams) => [...RecordKeys.lists(), { search: params.search, page: params.search }] as const,', [whole('params')]],
    ['a method that reads a field', 'list(params: RecordListParams) { return [...RecordKeys.lists(), { search: params.search }] as const },', [whole('params')]],
    ['a spread into an object', 'list: (params: RecordListParams) => [...RecordKeys.lists(), { ...params }] as const,', [whole('params')]],
    ['a spread into the key', 'byIds: (ids: string[]) => [...RecordKeys.all, ...ids] as const,', [whole('ids')]],
    ['a parameter in an object', 'search: (search: string) => [...RecordKeys.lists(), { search }] as const,', [whole('search')]],
    ['a call on the parameter', 'day: (date: Date) => [...RecordKeys.all, date.toISOString()] as const,', [whole('date')]],
    ['the parameter given to a call', 'list: (params: RecordListParams) => listKey(params),', [whole('params')]],
    ['the whole parameter and a field of it', 'list: (params: RecordListParams) => [...RecordKeys.lists(), params, params.search] as const,', [whole('params')]],
    ['a parameter that the key does not use', 'detail: (id: string, tab: string) => [...RecordKeys.all, id] as const,', [whole('tab')]],
    ['a branch that returns a key with no parameter', 'list: (params: RecordListParams) => { if (flag) return [...RecordKeys.lists()] as const; return [...RecordKeys.lists(), params] as const },', [whole('params')]],
    ['a choice between two keys', 'list: (params: RecordListParams) => (flag ? [...RecordKeys.lists(), params] : [...RecordKeys.lists()]),', [whole('params')]],
  ])('reports %s', (_, property, expected) => {
    expect(serverStateMessages(keysOf(property), keysFile)).toEqual(expected)
  })

  it('reports a function outside the object of the keys', () => {
    const code = `${keysOf('list: (params: RecordListParams) => listKey(params),')}
export function listKey(params: RecordListParams) {
  return [...RecordKeys.lists(), { search: params.search }] as const
}`

    expect(serverStateMessages(code, keysFile)).toEqual([whole('params'), whole('params')])
  })

  it('takes each parameter whole as an item of the returned key', () => {
    const code = keysOf(`list: (params: RecordListParams) => [...RecordKeys.lists(), params] as const,
  search: (search: string) => [...RecordKeys.lists(), 'search', search],
  page: (params: RecordListParams, page: number) => [...RecordKeys.lists(), params, page] satisfies readonly unknown[],
  details: () => [...RecordKeys.all, 'detail'] as const,
  detail(id: string) { return [...RecordKeys.all, 'detail', id] as const },
  named: function named(id: string) { return [...RecordKeys.all, id] as const },`)

    expect(serverStateMessages(code, keysFile)).toEqual([])
  })
})

describe('words made from a code', () => {
  // The config of the app, so that a later block that replaces no-restricted-syntax for one of these files fails the test
  // Most of these files are not on disk, so the TypeScript program of messages/failure-text does not have them.
  const messagesFor = async (code, filePath = 'src/zone.ts') => {
    const eslint = new ESLint({
      cwd: import.meta.dirname,
      overrideConfig: { languageOptions: { parserOptions: { projectService: false } }, rules: { 'messages/failure-text': 'off' } },
    })
    const [result] = await eslint.lintText(code, { filePath })
    return result.messages.map((message) => message.message)
  }

  it.each([
    "export const where = (zone) => zone.split('/').pop().replaceAll('_', ' ')",
    "export const where = (zone) => zone.replace(/_/g, ' ')",
    "export const where = (zone) => zone.replace('_', ' ')",
    "export const where = (zone) => zone.replaceAll('_', ` `)",
    "export const where = (zone) => zone.replace(/_+/g, ' ')",
    "export const where = (zone) => zone.split('_').join(' ')",
  ])('reports %s', async (code) => {
    expect(await messagesFor(code)).toEqual([codeToWordsRules[0].message])
  })

  it.each(['src/lib/zoned-time.ts', 'src/components/zone.tsx', 'src/examples/zone.tsx'])('reports it in %s', async (filePath) => {
    expect(await messagesFor("export const where = (zone: string) => zone.replaceAll('_', ' ')", filePath)).toEqual([codeToWordsRules[0].message])
  })

  it('accepts a replace that does not make words from a code', async () => {
    expect(await messagesFor("export const slug = (text) => text.replaceAll(' ', '_')")).toEqual([])
  })
})

describe('messages/failure-text', { timeout: 60_000 }, () => {
  // The project service gives each file the text that the test lints, so the path must be a file of the app.
  const fileThatTheExampleWipeKeeps = 'src/components/query-error.tsx'
  const failureTextMessages = async (code, filePath = fileThatTheExampleWipeKeeps) => {
    // With CI=true, typescript-eslint infers a single run. It then takes each lint of a path after the first for a --fix
    // pass and gives it a program with no types. Each test lints the same path.
    const eslint = new ESLint({
      cwd: import.meta.dirname,
      overrideConfig: { languageOptions: { parserOptions: { disallowAutomaticSingleRunInference: true } } },
    })
    const [result] = await eslint.lintText(code, { filePath })
    return result.messages.filter((message) => message.ruleId === 'messages/failure-text').map((message) => message.messageId)
  }
  const imports = "import { ProblemDetailsError } from '@/lib/problem-details'\nimport { failureText } from '@/lib/request-failure'\n"

  it.each([
    ['an error that a catch narrows', 'export const text = (error: unknown) => (error instanceof Error ? error.message : "Failed.")'],
    ['an error of the Server', 'export const text = (error: ProblemDetailsError) => error?.message'],
    ['a prop of the type Error', 'export const text = ({ error }: { error: Error | null }) => error?.message ?? ""'],
    ['an error that a type parameter holds', 'export const text = <E extends Error>(error: E) => error.message'],
    ['an error that a declaration takes apart', 'export function text(error: Error) {\n  const { message } = error\n  return message\n}'],
    ['an error that a parameter takes apart', 'export const text = ({ message }: ProblemDetailsError) => message'],
    ['a value that a check of its shape narrows', 'export const text = (error: unknown) => (error && typeof error === "object" && "message" in error ? String(error.message) : "")'],
    ['a value that a cast gives a message', 'export const text = (error: unknown) => (error as { message: string }).message'],
  ])('reports the message of %s', async (_, code) => {
    expect(await failureTextMessages(imports + code)).toEqual(['message'])
  })

  it('accepts the message of a field error of a form, which is not an Error', async () => {
    const code = 'export const text = (fieldState: { error?: { type: string; message?: string } }) => fieldState.error?.message'
    expect(await failureTextMessages(code)).toEqual([])
  })

  it('reports the detail and the title of a problem, and accepts the title of another type', async () => {
    const code = `${imports}import type { ValidationProblemDetails } from '@/api/generated/models/types/validation-problem-details'
export const text = (error: ProblemDetailsError, refusal: ValidationProblemDetails, page: { title: string }) =>
  error.problemDetails.detail ?? error.problemDetails.title ?? refusal.detail ?? error.problemDetails['title'] ?? page.title
export function parts(error: ProblemDetailsError) {
  const { detail, status } = error.problemDetails
  return [detail, status]
}`
    expect(await failureTextMessages(code)).toEqual(['problemText', 'problemText', 'problemText', 'problemText', 'problemText'])
  })

  it('reports an error that carries a text to the screen, and accepts an error that the code throws', async () => {
    const code = `${imports}export function show(error: unknown, onError: (error: unknown) => void, ok: boolean) {
  onError(error instanceof ProblemDetailsError ? error : new Error('The invite was not created. Check your connection, then try again.'))
  if (!ok) throw ok === false ? new Error('A fault in the code.') : error
  if (ok) throw new Error('A fault in the code.') satisfies Error
}
export const later = () => setTimeout(() => show(Error('Check your connection.'), () => {}, true))
export const fail = () => Promise.reject(new Error('A fault in the code.'))
export const wait = () => new Promise((_, reject) => reject(new Error('A fault in the code.')))`
    expect(await failureTextMessages(code)).toEqual(['newError', 'newError'])
  })

  it('reports a root error of a form with a text of its own, and accepts failureText', async () => {
    const code = `${imports}type Form = { setError: (name: string, error: { message: string }) => void }
export function show(form: Form, error: unknown) {
  form.setError('root.server', { message: 'The tenant was not started. Check your connection, then try again.' })
  form.setError('root.server', { message: failureText(error) })
  form.setError('name', { message: 'Enter a name.' })
}
export function showOnItsOwn(setError: Form['setError']) {
  setError(\`root.server\`, { message: 'Check your connection.' })
  setError('root', { 'message': 'Check your connection.' })
  setError('rootFolder', { message: 'Enter a folder.' })
}`
    expect(await failureTextMessages(code)).toEqual(['rootError', 'rootError', 'rootError'])
  })

  it('lets the owner read the message', async () => {
    expect(await failureTextMessages('export const text = (error: Error) => error.message', 'src/lib/request-failure.ts')).toEqual([])
  })
})

const SEED_PATH_WITHOUT_TYPE_INFO = 'conformance/seed.ts'

const antiSlopSeeds = {
  'anti-slop/no-accumulating-spread': 'export const all = [1, 2].reduce((acc: number[], n) => [...acc, n], [])\n',
  'anti-slop/no-array-filter-map': 'const xs: number[] = [1, 2]\nexport const ys = xs.filter((x) => x > 1).map((x) => x * 2)\n',
  'anti-slop/no-chained-type-assertions': 'declare const v: string\nexport const n = v as unknown as number\n',
  'anti-slop/no-conditional-empty-object-spread': 'export const f = (t?: number) => ({ ...(t !== undefined ? { t } : {}) })\n',
  'anti-slop/no-known-value-widening': 'export const m: Record<string, number> = { a: 1 }\n',
  'anti-slop/no-module-mocking': "import { vi } from 'vitest'\nvi.mock('./x')\n",
  'anti-slop/no-object-parameters': 'export function save(value: object) {\n  return value\n}\n',
  'anti-slop/no-reduce-accumulator-copy': 'export const all = [1].reduce((acc, n) => Object.assign({}, acc, { [n]: n }), {})\n',
  'anti-slop/no-reflect-apply': 'export const r = Reflect.apply(Math.max, null, [1])\n',
  'anti-slop/no-reflect-get': "export const g = Reflect.get(Math, 'PI')\n",
  'anti-slop/no-runtime-typeof': "export const isText = (v: string | number) => typeof v === 'string'\n",
  'anti-slop/no-shape-in-symbol-names': 'export interface UserShape {\n  id: string\n}\n',
  'anti-slop/no-unknown-parameters': 'export function handle(input: unknown) {\n  return input\n}\n',
  'anti-slop/no-unknown-returns': 'export function load(): unknown {\n  return 1\n}\n',
  'anti-slop/no-unknown-type-aliases': 'export type External = unknown\n',
  'anti-slop/no-unsafe-dictionary-type': 'export type Metadata = Record<string, unknown>\n',
  'anti-slop/no-widen-then-assert': 'const loaded: number = 1\nconst stored: unknown = loaded\nexport const n = stored as number\n',
  'anti-slop/require-readable-spacing': 'export const first = 1\n/** The second. */\nexport const second = 2\n',
  'anti-slop/require-safety-comment-for-type-assertion': 'declare const v: string | number\nexport const n = v as number\n',
}

async function antiSlopRulesOf(code) {
  const eslint = new ESLint({ cwd: import.meta.dirname })
  const [result] = await eslint.lintText(code, { filePath: SEED_PATH_WITHOUT_TYPE_INFO })
  return result.messages.map((message) => message.ruleId)
}

describe('anti-slop rules', { timeout: 60_000 }, () => {
  it.each(Object.keys(antiSlopRules))('%s reports its seeded violation', async (rule) => {
    expect(antiSlopSeeds[rule], `add a seed for ${rule}`).toBeDefined()
    expect(await antiSlopRulesOf(antiSlopSeeds[rule])).toContain(rule)
  })

  it.each(['src/seed.tsx', 'tests/seed.ts', 'e2e/seed.ts'])('applies each rule to %s', async (file) => {
    const config = await new ESLint({ cwd: import.meta.dirname }).calculateConfigForFile(file)
    for (const rule of Object.keys(antiSlopRules))
      expect(config.rules[rule]?.[0], rule).toBe(2)
  })

})

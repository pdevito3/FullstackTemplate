import { ESLint, Linter } from 'eslint'
import tseslint from 'typescript-eslint'
import { describe, expect, it } from 'vitest'
import { codeToWordsRules, noPageActionRule, serverStatePlugin, stringSelectorMessage } from './eslint.config.js'

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
  const messagesFor = async (code, filePath = 'src/zone.ts') => {
    const eslint = new ESLint({ cwd: import.meta.dirname })
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

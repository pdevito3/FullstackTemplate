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
    ['useInfiniteQuery({ queryKey: ["x", search], queryFn: ({ pageParam, signal }) => get({ search, pageParam, signal }), initialPageParam: 1, getNextPageParam: () => 2 })', [], 'search'],
  ])('checks the probe %s', (call, names, parameters) => {
    const code = `const PAGE_SIZE = 10\nfunction useProbe(${parameters}) { return ${call} }`

    expect(serverStateMessages(code)).toEqual(names.map((name) => `query-key-has-inputs: The query function reads \`${name}\``))
  })
})

describe('server-state/key-has-each-param', () => {
  it('reports a parameter that the key takes apart when the pattern cannot show each field', () => {
    const code = `export const RecordKeys = {
      all: ['records'] as const,
      list: ({ search }: RecordListParams) => [...RecordKeys.all, 'list', { search }] as const,
      untyped: ({ search }) => [...RecordKeys.all, 'list', { search }] as const,
    }`

    expect(serverStateMessages(code, 'src/domain/records/apis/record.keys.ts')).toEqual([
      'key-has-each-param: The key takes apart a parameter of type `RecordListParams`',
      'key-has-each-param: The key takes apart a parameter of type `unknown`',
    ])
  })

  it('reports a field of an inline type that the pattern leaves out', () => {
    const code = `export const RecordKeys = {
      all: ['records'] as const,
      list: ({ search }: { search: string; page?: number }, page: number) => [...RecordKeys.all, search] as const,
    }`

    expect(serverStateMessages(code, 'src/domain/records/apis/record.keys.ts')).toEqual([
      'key-has-each-param: The key leaves out `page` of this parameter',
      'key-has-each-param: The key uses only a part of `page`',
    ])
  })

  it('takes a pattern with each field of its inline type, and a rest element that the key uses whole', () => {
    const code = `export const RecordKeys = {
      all: ['records'] as const,
      list: ({ search, page }: { search: string; page: number }) => [...RecordKeys.all, { search, page }] as const,
      rest: ({ search, ...rest }: RecordListParams) => [...RecordKeys.all, search, rest] as const,
      fields: (params: { search: string; page: number }) => [...RecordKeys.all, { search: params.search, page: params.page }] as const,
    }`

    expect(serverStateMessages(code, 'src/domain/records/apis/record.keys.ts')).toEqual([])
  })

  it('reports a parameter that the key reads only through a field, or does not read', () => {
    const code = `export const RecordKeys = {
      all: ['records'] as const,
      list: (params: RecordListParams) => [...RecordKeys.all, 'list', params.search] as const,
      detail: (id: string, tab: string) => [...RecordKeys.all, 'detail', id] as const,
    }`

    expect(serverStateMessages(code, 'src/domain/records/apis/record.keys.ts')).toEqual([
      'key-has-each-param: The key uses only a part of `params`',
      'key-has-each-param: The key uses only a part of `tab`',
    ])
  })

  it('takes a key with each whole parameter', () => {
    const code = `export const RecordKeys = {
      all: ['records'] as const,
      list: (params: RecordListParams) => [...RecordKeys.all, 'list', params] as const,
      search: (search: string) => [...RecordKeys.all, 'search', { search }] as const,
    }`

    expect(serverStateMessages(code, 'src/domain/records/apis/record.keys.ts')).toEqual([])
  })

  it('takes a method call on a whole parameter, a callback in a key, and a named function', () => {
    const code = `export const RecordKeys = {
      all: ['records'] as const,
      byIds: (ids: string[]) => [...RecordKeys.all, 'ids', [...ids].sort((a, b) => a.localeCompare(b)).join(',')] as const,
      day: (date: Date) => [...RecordKeys.all, 'day', date.toISOString()] as const,
      named: function named(id: string) { return [...RecordKeys.all, id] as const },
    }`

    expect(serverStateMessages(code, 'src/domain/records/apis/record.keys.ts')).toEqual([])
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

import { readFileSync } from 'node:fs'

import { defineConfig } from 'kubb/config'
import { pluginAxios } from '@kubb/plugin-axios'
import { pluginTs } from '@kubb/plugin-ts'
import { pluginZod } from '@kubb/plugin-zod'

const input = '../FullstackTemplate.Server/openapi/FullstackTemplate.Server.json'

// Kubb gives a schema file and an operation file the same file request, so the schema names tell them apart.
const schemaNames = new Set(
  Object.keys(JSON.parse(readFileSync(input, 'utf8')).components?.schemas ?? {}),
)

const kebabCase = (name: string) =>
  name
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1-$2')
    .replace(/[\s_]+/g, '-')
    .toLowerCase()

// One file per operation at <tag>/<kind>/<operation>.ts, and one file per shared schema at models/<kind>/<schema>.ts.
const layout = (kind: 'types' | 'zod' | 'clients') => ({
  output: { path: '.', mode: 'directory', barrel: false } as const,
  group: { type: 'tag', name: ({ group }: { group: string }) => `${kebabCase(group)}/${kind}` } as const,
  resolver: {
    file: {
      baseName: ({ name, extname }: { name: string; extname: `.${string}` }) =>
        schemaNames.has(name)
          ? (`models/${kind}/${kebabCase(name)}${extname}` as const)
          : (`${kebabCase(name)}${extname}` as const),
    },
  },
})

export default defineConfig({
  input,
  output: { path: './src/api/generated', clean: true, barrel: false },
  plugins: [
    pluginTs(layout('types')),
    pluginZod(layout('zod')),
    pluginAxios({
      ...layout('clients'),
      returnType: 'data',
      // A list endpoint sends its paging metadata in the X-Pagination header, so its client returns the full response.
      override: [{ type: 'operationId', pattern: /List$/, options: { returnType: 'full' } }],
    }),
  ],
})

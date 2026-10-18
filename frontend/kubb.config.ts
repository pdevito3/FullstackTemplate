import { readdirSync, readFileSync } from 'node:fs'

import { defineConfig } from 'kubb/config'
import { pluginAxios } from '@kubb/plugin-axios'
import { pluginTs } from '@kubb/plugin-ts'
import { pluginZod } from '@kubb/plugin-zod'

// Kubb sends usage data to otlp.kubb.dev after each run. It reads this variable when it sends, after it loads this file.
// A variable in the package.json script does not work on Windows, so the config sets it.
process.env.KUBB_DISABLE_TELEMETRY = '1'

// A Debug build of the Server writes one OpenAPI document for each API version:
// FullstackTemplate.Server.json for v1, FullstackTemplate.Server_v2.json for v2, and more.
const openApiFolder = '../FullstackTemplate.Server/openapi'

// Kubb empties each output folder before it generates, so each document needs its own folder, not a folder inside another folder.
// lib/api-client.ts connects the client of each src/api/generated* folder to apiClient.
function outputFor(file: string) {
  if (file === 'FullstackTemplate.Server.json') return './src/api/generated'
  const version = /^FullstackTemplate\.Server_v(\d+)\.json$/.exec(file)?.[1]
  if (!version) throw new Error(`${openApiFolder}/${file} is not an OpenAPI document that kubb.config.ts knows.`)
  return `./src/api/generated-v${version}`
}

const documents = readdirSync(openApiFolder)
  .filter((file) => file.endsWith('.json'))
  .sort()
  .map((file) => ({ input: `${openApiFolder}/${file}`, output: outputFor(file) }))

const kebabCase = (name: string) =>
  name
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1-$2')
    .replace(/[\s_]+/g, '-')
    .toLowerCase()

function configFor({ input, output }: { input: string; output: string }) {
  // Kubb gives a schema file and an operation file the same file request, so the schema names tell them apart.
  const schemaNames = new Set(
    Object.keys(JSON.parse(readFileSync(input, 'utf8')).components?.schemas ?? {}),
  )

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

  return {
    input,
    output: { path: output, clean: true, barrel: false } as const,
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
  }
}

export default defineConfig(documents.map(configFor))

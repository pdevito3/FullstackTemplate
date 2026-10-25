/**
 * Reads the stack of this checkout from the result of `dotnet aspire describe --format Json`. e2e/run-against-stack.ts
 * runs the command and gives the result to checkStack.
 */
import type { SpawnSyncReturns } from 'node:child_process'
import * as z from 'zod'

const resourceSchema = z.looseObject({
  displayName: z.string(),
  state: z.string().nullish(),
  healthStatus: z.string().nullish(),
  urls: z.array(z.looseObject({ name: z.string(), url: z.string() })).nullish(),
})

const descriptionSchema = z.object({ resources: z.array(resourceSchema) })

export type StackResource = z.infer<typeof resourceSchema>

export type DescribeRun = Pick<SpawnSyncReturns<string>, 'status' | 'stdout' | 'error'>

export type StackCheck = { kind: 'ready'; resources: StackResource[] } | { kind: 'not-ready'; message: string }

export function checkStack({ run }: { run: DescribeRun; appHost: string }): StackCheck {
  return { kind: 'ready', resources: (JSON.parse(run.stdout) as z.infer<typeof descriptionSchema>).resources }
}

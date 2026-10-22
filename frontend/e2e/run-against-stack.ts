/**
 * Runs the Playwright e2e tests against the Aspire stack of this checkout.
 * An isolated stack has random ports, so this script reads the URLs from `dotnet aspire describe`.
 * The IDP is the one whose resource is in the stack, or the Duende demo if the stack has no IdP resource.
 * Set IDP to override it. Extra arguments go to `playwright test`, for example `pnpm test:e2e:stack --repeat-each=5`.
 * See docs/authentication.md for the command that starts the stack.
 */
import { execFileSync, spawnSync } from 'node:child_process'
import { idpNames, loadIdp } from './idps.ts'

interface DescribedResource {
  displayName: string
  urls?: { name: string; url: string }[]
}

const described = JSON.parse(
  execFileSync('dotnet', ['aspire', 'describe', '--format', 'Json', '--apphost', '../FullstackTemplate.AppHost'], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'inherit'],
  }),
) as { resources: DescribedResource[] }

function endpointUrl(resource: string, endpoint: string): string {
  const url = described.resources
    .find((r) => r.displayName === resource)
    ?.urls?.find((u) => u.name === endpoint)?.url
  if (!url) {
    throw new Error(`The stack has no ${endpoint} URL for '${resource}'. Start the stack with the AuthProvider of the IDP.`)
  }
  return url
}

function idpOfStack(): string {
  const resources = new Set(described.resources.map((r) => r.displayName))
  const idps = idpNames().map((name) => ({ name, resource: loadIdp(name).resource }))
  const detected =
    idps.find((idp) => idp.resource !== undefined && resources.has(idp.resource)) ??
    idps.find((idp) => idp.resource === undefined)
  if (!detected) {
    throw new Error(`The stack has no resource of an IDP in e2e/idps/. Set IDP to one of these: ${idpNames().join(', ')}.`)
  }
  return detected.name
}

const env = {
  ...process.env,
  IDP: process.env.IDP ?? idpOfStack(),
  BASE_URL: endpointUrl('webfrontend', 'http'),
  SERVER_URL: endpointUrl('server', 'https'),
}
console.log(`BASE_URL=${env.BASE_URL} SERVER_URL=${env.SERVER_URL} IDP=${env.IDP}`)

const result = spawnSync('pnpm', ['exec', 'playwright', 'test', ...process.argv.slice(2)], { stdio: 'inherit', env })
process.exit(result.status ?? 1)

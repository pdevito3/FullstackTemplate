/**
 * Runs the Playwright e2e tests against the Aspire stack of this checkout, with Keycloak as the IDP.
 * An isolated stack has random ports, so this script reads the URLs from `dotnet aspire describe`.
 * Extra arguments go to `playwright test`, for example `pnpm test:e2e:keycloak --repeat-each=5`.
 * See docs/authentication.md for the command that starts the stack.
 */
import { execFileSync, spawnSync } from 'node:child_process'

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
    throw new Error(`The stack has no ${endpoint} URL for '${resource}'. Start the stack with AuthProvider=Keycloak.`)
  }
  return url
}

const env = {
  ...process.env,
  IDP: 'keycloak',
  BASE_URL: endpointUrl('webfrontend', 'http'),
  SERVER_URL: endpointUrl('server', 'https'),
}
console.log(`BASE_URL=${env.BASE_URL} SERVER_URL=${env.SERVER_URL} IDP=${env.IDP}`)

const result = spawnSync('pnpm', ['exec', 'playwright', 'test', ...process.argv.slice(2)], { stdio: 'inherit', env })
process.exit(result.status ?? 1)

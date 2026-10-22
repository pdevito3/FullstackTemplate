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

function environmentOf(resource: string): Record<string, string> {
  return (described.resources.find((r) => r.displayName === resource) as { environment?: Record<string, string> } | undefined)
    ?.environment ?? {}
}

// The authority of the BFF
function bffAuthority(): string {
  const authority = environmentOf('bff').Auth__Authority
  if (!authority) {
    throw new Error('The stack has no Auth__Authority for the BFF.')
  }
  return authority
}

// `dotnet aspire describe` hides the value of a secret parameter, so read it from the user secrets of the AppHost,
// where the AppHost keeps each generated value. The value is the last line of the output.
function appHostSecret(key: string): string {
  const output = execFileSync('dotnet', ['aspire', 'secret', 'get', key, '--non-interactive', '--nologo', '--apphost', '../FullstackTemplate.AppHost'], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'inherit'],
  })
  return output.trim().split('\n').at(-1)?.trim() ?? ''
}

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

const idp = (process.env.IDP ?? idpOfStack()).toLowerCase()
const bffEnvironment = environmentOf('bff')
const env: NodeJS.ProcessEnv = {
  ...process.env,
  IDP: idp,
  BASE_URL: endpointUrl('webfrontend', 'http'),
  SERVER_URL: endpointUrl('server', 'https'),
  AUTHORITY: bffAuthority(),
  // The refresh rotation test uses the client of the BFF for its own code flow against the identity provider
  CLIENT_ID: bffEnvironment.Auth__ClientId,
  CLIENT_SECRET: bffEnvironment.Auth__ClientSecret,
}

// The back-channel logout test ends the session of the user through the admin API of the identity provider. The
// script reads only the admin variables of the provider, and it never prints a secret.
if (idp === 'keycloak') {
  const keycloak = environmentOf('keycloak')
  env.IDP_URL = endpointUrl('keycloak', 'http')
  env.IDP_ADMIN_USERNAME = keycloak.KC_BOOTSTRAP_ADMIN_USERNAME
  env.IDP_ADMIN_PASSWORD = keycloak.KC_BOOTSTRAP_ADMIN_PASSWORD
} else if (idp === 'authentik') {
  env.IDP_URL = endpointUrl('authentik', 'http')
  env.IDP_ADMIN_TOKEN = appHostSecret('Parameters:authentik-bootstrap-token')
}

console.log(`BASE_URL=${env.BASE_URL} SERVER_URL=${env.SERVER_URL} AUTHORITY=${env.AUTHORITY} IDP=${env.IDP}`)

const result = spawnSync('pnpm', ['exec', 'playwright', 'test', ...process.argv.slice(2)], { stdio: 'inherit', env })
process.exit(result.status ?? 1)

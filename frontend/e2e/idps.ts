/**
 * The login form of each IdP. Each IdP is one JSON file in e2e/idps/, named after the AuthProvider of the AppHost in
 * lowercase. To delete a provider, delete its file and its folder in the AppHost.
 */
import { readdirSync, readFileSync } from 'node:fs'

export interface IdpConfig {
  name: string
  // The Aspire resource of the IdP. A provider outside the stack, such as the Duende demo, has none.
  resource?: string
  email?: string
  username?: string
  // A passwordless IdP, such as 0factor, has no password and no password field
  password?: string
  // Selectors for login form (vary by IDP)
  usernameSelector: string
  passwordSelector?: string
  submitSelector: string
  // Some IDPs need special handling
  needsOtpSkip?: boolean
  // Authentik uses a two-step login (username first, then password)
  twoStepLogin?: boolean
  // Second step submit selector for two-step login (if different from submitSelector)
  secondStepSubmitSelector?: string
  // The provider features that the e2e tests expect (section 6 of docs/specs/home-rolled-bff.md)
  capabilities: IdpCapabilities
  // The provider features that discovery does not show
  sessionFeatures: IdpSessionFeatures
}

/**
 * A feature test runs only for a provider that has the feature. For the other providers, a test proves the fallback.
 */
export interface IdpCapabilities {
  // Discovery lists revocation_endpoint (RFC 7009)
  revocation: boolean
  // Discovery lists authorization_response_iss_parameter_supported (RFC 9207)
  issuerParameter: boolean
  // Discovery lists backchannel_logout_supported. The Duende Demo lists it, but it cannot reach the BFF.
  backChannelLogout: boolean
  // Discovery lists end_session_endpoint
  endSession: boolean
  // Discovery lists pushed_authorization_request_endpoint, so the BFF pushes the authorize request (RFC 9126)
  pushedAuthorization: boolean
}

/**
 * The provider features that discovery does not show (section 6 of docs/specs/home-rolled-bff.md).
 */
export interface IdpSessionFeatures {
  // The provider sends the back-channel logout token to the BFF. The Duende Demo cannot reach the BFF. FusionAuth
  // does not send it, and Cognito and ZeroFactor do not support it.
  backChannelLogoutReachesBff: boolean
  // Each refresh gives a new refresh token, and a reuse of the old one gets invalid_grant. Cognito gives no new
  // refresh token. ZeroFactor gives a new one, but its refresh tokens are stateless, so the old one still works.
  refreshRotation: boolean
}

const idpsDir = new URL('./idps/', import.meta.url)

export function idpNames(): string[] {
  return readdirSync(idpsDir)
    .filter((file) => file.endsWith('.json'))
    .map((file) => file.slice(0, -'.json'.length))
}

export function loadIdp(name: string | undefined): IdpConfig {
  if (!name) {
    throw new Error('IDP is not set. Run the tests with `pnpm test:e2e:stack`, which finds the IdP of the running stack.')
  }
  const key = name.toLowerCase()
  if (!idpNames().includes(key)) {
    throw new Error(`IDP '${name}' has no file in e2e/idps/. Use one of these: ${idpNames().join(', ')}.`)
  }
  return JSON.parse(readFileSync(new URL(`${key}.json`, idpsDir), 'utf8')) as IdpConfig
}

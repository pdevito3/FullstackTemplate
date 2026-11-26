/**
 * The login form of each IdP. Each IdP is one JSON file in e2e/idps/, named after the AuthProvider of the AppHost in
 * lowercase. To delete a provider, delete its file and its folder in the AppHost.
 */
import { readdirSync, readFileSync } from 'node:fs'
import * as z from 'zod'

const idpCapabilitiesSchema = z.object({
  // Discovery lists revocation_endpoint (RFC 7009)
  revocation: z.boolean(),
  // Discovery lists authorization_response_iss_parameter_supported (RFC 9207)
  issuerParameter: z.boolean(),
  // Discovery lists backchannel_logout_supported. The Duende Demo lists it, but it cannot reach the BFF.
  backChannelLogout: z.boolean(),
  // Discovery lists end_session_endpoint
  endSession: z.boolean(),
  // Discovery lists pushed_authorization_request_endpoint, so the BFF pushes the authorize request (RFC 9126)
  pushedAuthorization: z.boolean(),
  // Discovery lists dpop_signing_alg_values_supported, so the BFF can bind the tokens to its key (RFC 9449). Keycloak
  // lists it because the AppHost starts it with --features=dpop. FusionAuth and Authentik list it too, but the AppHost
  // runs DPoP only with a provider that sets TestedWithDPoP, so the DPoP tests do not run against them.
  dpop: z.boolean(),
})

const idpSessionFeaturesSchema = z.object({
  // The provider sends the back-channel logout token to the BFF. The Duende Demo cannot reach the BFF. FusionAuth
  // does not send it, and Cognito and ZeroFactor do not support it.
  backChannelLogoutReachesBff: z.boolean(),
  // Each refresh gives a new refresh token, and a reuse of the old one gets invalid_grant. Cognito gives no new
  // refresh token. ZeroFactor gives a new one, but its refresh tokens are stateless, so the old one still works.
  refreshRotation: z.boolean(),
})

const idpConfigSchema = z.object({
  name: z.string(),
  // The Aspire resource of the IdP. A provider outside the stack, such as the Duende demo, has none.
  resource: z.string().optional(),
  email: z.string().optional(),
  username: z.string().optional(),
  // A passwordless IdP, such as 0factor, has no password and no password field
  password: z.string().optional(),
  // Selectors for login form (vary by IDP)
  usernameSelector: z.string(),
  passwordSelector: z.string().optional(),
  submitSelector: z.string(),
  // Some IDPs need special handling
  needsOtpSkip: z.boolean().optional(),
  // Authentik uses a two-step login (username first, then password)
  twoStepLogin: z.boolean().optional(),
  // Second step submit selector for two-step login (if different from submitSelector)
  secondStepSubmitSelector: z.string().optional(),
  // The provider features that the e2e tests expect (section 6 of docs/specs/home-rolled-bff.md)
  capabilities: idpCapabilitiesSchema,
  // The provider features that discovery does not show
  sessionFeatures: idpSessionFeaturesSchema,
})

export type IdpConfig = z.infer<typeof idpConfigSchema>

export type IdpCapabilities = z.infer<typeof idpCapabilitiesSchema>

export type IdpSessionFeatures = z.infer<typeof idpSessionFeaturesSchema>

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

  return idpConfigSchema.parse(JSON.parse(readFileSync(new URL(`${key}.json`, idpsDir), 'utf8')))
}

/**
 * The rules for the features of the running stack. e2e/run-against-stack.ts and the tests use the same rules, so
 * that the expectation check of a CI leg and the skip of a test cannot disagree.
 */

// The access token lifetime of the providers in the stack
const accessTokenLifetimeSeconds = 3600

/**
 * True when the BFF refreshes on each API call: the refresh margin of the BFF (Auth__RefreshBeforeExpirationSeconds)
 * is longer than the access token lifetime.
 */
export function refreshesOnEachCall(refreshMarginSeconds: string | undefined): boolean {
  return Number(refreshMarginSeconds ?? '0') > accessTokenLifetimeSeconds
}

export const refreshMarginHint = `The BFF refreshes on each API call only when the stack starts with BffRefreshBeforeExpirationSeconds above the access token lifetime of ${accessTokenLifetimeSeconds} s, for example BffRefreshBeforeExpirationSeconds=86400 (docs/authentication.md).`

import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import type { AuthState, BffClaim } from '../types'
import { AuthKeys } from './auth.keys'

/**
 * Fetch user claims from BFF
 */
export async function getUser(): Promise<BffClaim[]> {
  const response = await apiClient.get<BffClaim[]>('/bff/user')
  return response.data
}

/**
 * Claim types by auth provider:
 *
 * FusionAuth:
 *   - email: user's email address (primary identifier)
 *   - sub: user ID (GUID)
 *   - authenticationType: "APPLICATION_TOKEN"
 *
 * Duende Demo:
 *   - name: display name ("Bob Smith")
 *   - given_name, family_name: name components
 *   - sub: user ID
 *   - idp: identity provider
 *   - amr: authentication methods
 *
 * Common:
 *   - bff:logout_url: logout endpoint
 *   - bff:session_expires_in: session TTL
 */

type ClaimFinder = (claims: BffClaim[]) => string | undefined

const findClaim =
  (type: string): ClaimFinder =>
  (claims) =>
    claims.find((c) => c.type === type)?.value

/**
 * Username claim finders in priority order.
 * Each provider may use different claims for the display name.
 */
const usernameFinders: ClaimFinder[] = [
  findClaim('name'), // Duende: full display name
  findClaim('email'), // FusionAuth: email as identifier
  findClaim('preferred_username'), // OIDC standard
  findClaim('sub'), // Fallback: subject identifier
]

/**
 * Parse BFF claims into a usable auth state.
 * Handles claims from any OIDC provider (FusionAuth, Duende, etc.)
 */
function parseAuthClaims(claims: BffClaim[]): Omit<AuthState, 'isLoading'> {
  const username = usernameFinders.reduce<string | undefined>(
    (found, finder) => found ?? finder(claims),
    undefined
  )
  const logoutUrl = findClaim('bff:logout_url')(claims)

  return {
    isLoggedIn: true,
    username: username ?? null,
    logoutUrl: logoutUrl ?? '/bff/logout',
  }
}

/**
 * Hook for fetching and managing auth state
 */
export function useAuth(): AuthState {
  const { data, isLoading, isError } = useQuery({
    queryKey: AuthKeys.user(),
    queryFn: getUser,
    retry: false, // Don't retry on 401/403
    staleTime: 5 * 60 * 1000, // Consider auth data fresh for 5 minutes
  })

  if (isLoading) {
    return {
      isLoggedIn: false,
      isLoading: true,
      username: null,
      logoutUrl: null,
    }
  }

  if (isError || !data) {
    return {
      isLoggedIn: false,
      isLoading: false,
      username: null,
      logoutUrl: null,
    }
  }

  return {
    ...parseAuthClaims(data),
    isLoading: false,
  }
}

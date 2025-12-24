import { useQuery } from '@tanstack/react-query'
import { weatherApi, authApi, type BffClaim } from './client'

/**
 * Hook for fetching public weather forecast data
 */
export function useWeatherForecast() {
  return useQuery({
    queryKey: ['weather'],
    queryFn: weatherApi.getForecasts,
  })
}

/**
 * Hook for fetching secure weather forecast data (requires authentication)
 */
export function useSecureWeatherForecast(enabled: boolean = true) {
  return useQuery({
    queryKey: ['weather', 'secure'],
    queryFn: weatherApi.getSecureForecasts,
    enabled,
    retry: false, // Don't retry on 401
  })
}

/**
 * Parsed auth state from BFF claims
 */
export interface AuthState {
  isLoggedIn: boolean
  isLoading: boolean
  username: string | null
  logoutUrl: string | null
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
    queryKey: ['auth', 'user'],
    queryFn: authApi.getUser,
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

/**
 * Auth action helpers
 */
export const useAuthActions = () => ({
  login: authApi.login,
  logout: authApi.logout,
})

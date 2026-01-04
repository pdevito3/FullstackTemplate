/**
 * Auth domain types
 */

export interface BffClaim {
  type: string
  value: string
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

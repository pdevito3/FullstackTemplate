/**
 * Auth action functions (non-API, browser navigation)
 */

/** Replaces the page in the history, so that Back from the login of the provider does not come back to a page that goes to the login again. */
export function login(returnUrl: string = '/'): void {
  window.location.replace(`/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`)
}

export function logout(logoutUrl?: string): void {
  window.location.href = logoutUrl || '/bff/logout'
}

/**
 * Hook for auth actions
 */
export const useAuthActions = () => ({
  login,
  logout,
})

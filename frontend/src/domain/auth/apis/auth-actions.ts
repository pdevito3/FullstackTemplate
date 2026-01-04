/**
 * Auth action functions (non-API, browser navigation)
 */

export function login(returnUrl: string = '/'): void {
  window.location.href = `/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`
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

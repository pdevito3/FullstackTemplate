/**
 * Auth action functions (non-API, browser navigation)
 */

import { forgetInviteToken } from '@/domain/invites/invite-link'

/** The BFF ignores a longer return URL, and goes to `/` (`ReturnUrl.MaxLength`). */
const maxReturnUrlLength = 2048

/**
 * The path, the query, and the hash of the page, as a return URL that the BFF accepts. The BFF refuses a path that
 * starts with `//` or `/\`. The browser percent-encodes each non-ASCII character of a location, so the length is the
 * length that the BFF measures.
 */
export function returnUrlOf({ pathname, search, hash }: Pick<Location, 'pathname' | 'search' | 'hash'>): string {
  if (pathname.startsWith('//') || pathname.startsWith('/\\')) return '/'
  return [pathname + search + hash, pathname].find((url) => url.length <= maxReturnUrlLength) ?? '/'
}

/** Replaces the page in the history, so that Back from the login of the provider does not come back to a page that goes to the login again. */
export function login(returnUrl: string = '/'): void {
  window.location.replace(`/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`)
}

export function logout(logoutUrl?: string): void {
  // The next person in this tab must not get the invite of this person. A session can also end with no logout in this
  // tab, so `inviteTokenFor` forgets the token too when another `sub` appears.
  forgetInviteToken()
  window.location.href = logoutUrl || '/bff/logout'
}

/**
 * Hook for auth actions
 */
export const useAuthActions = () => ({
  login,
  logout,
})

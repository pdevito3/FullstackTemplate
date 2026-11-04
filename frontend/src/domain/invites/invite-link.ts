const STORAGE_KEY = 'invite-token'

/** The link that an admin sends. The token is in the fragment, so the browser sends it to no server (INV-3). */
export const inviteLinkOf = (origin: string, token: string): string => `${origin}/join#token=${token}`

/** The value of `token` in a URL fragment such as `#token=abc`, or null when the fragment has no `token`. */
export const tokenOfFragment = (hash: string): string | null => new URLSearchParams(hash.slice(1)).get('token')

const isJoinPath = (pathname: string) => pathname.replace(/\/$/, '') === '/join'

function moveTokenToTheTab(): boolean {
  const token = isJoinPath(window.location.pathname) ? tokenOfFragment(window.location.hash) : null
  if (token === null) return false
  if (token) sessionStorage.setItem(STORAGE_KEY, token)
  else sessionStorage.removeItem(STORAGE_KEY)
  window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search)
  return true
}

/**
 * Moves the token of an invite link from the address bar to `sessionStorage`, which is for this tab only. There it
 * survives a reload and the redirects of the sign-in. Call this before the router and the session check read the URL,
 * because the session check sends the path, the query, and the hash to `/bff/login` as a query value.
 *
 * A second link that opens in the same tab changes only the fragment, so the page does not load by itself. The reload
 * shows the page of the new link.
 */
export function keepInviteTokenOutOfTheUrl(): void {
  moveTokenToTheTab()
  window.addEventListener('hashchange', () => {
    if (moveTokenToTheTab()) window.location.reload()
  })
}

export const readInviteToken = (): string | null => sessionStorage.getItem(STORAGE_KEY)

export const forgetInviteToken = (): void => sessionStorage.removeItem(STORAGE_KEY)

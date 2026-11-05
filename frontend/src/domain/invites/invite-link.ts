const TOKEN_KEY = 'invite-token'
const HOLDER_KEY = 'invite-token-holder'

/** The link that an admin sends. The token is in the fragment, so the browser sends it to no server (INV-3). */
export const inviteLinkOf = (origin: string, token: string): string => `${origin}/join#token=${token}`

/** The value of `token` in a URL fragment such as `#token=abc`, or null when the fragment has no `token`. */
export const tokenOfFragment = (hash: string): string | null => new URLSearchParams(hash.slice(1)).get('token')

/**
 * A fragment with a token is an invite link on each path, so the token goes to no server if an edit changed the path.
 * The address becomes the path of the join page, so a token never waits on another page, and this code does not have
 * to know which paths the router matches.
 */
function moveLinkToTheTab(): boolean {
  const token = tokenOfFragment(window.location.hash)
  if (token === null) return false
  forgetInviteToken()
  if (token) sessionStorage.setItem(TOKEN_KEY, token)
  window.history.replaceState(window.history.state, '', '/join')
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
  moveLinkToTheTab()
  window.addEventListener('hashchange', () => {
    if (moveLinkToTheTab()) window.location.reload()
  })
}

export const readInviteToken = (): string | null => sessionStorage.getItem(TOKEN_KEY)

export function forgetInviteToken(): void {
  sessionStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(HOLDER_KEY)
}

/**
 * The token of the tab, for the signed-in person with this `sub`. The first `sub` that asks holds the token. Another
 * `sub` is another person in the tab, for example after a session that ended with no logout in this tab, so the token
 * goes. A link that opens in the tab starts again with no holder, because that person has the link.
 */
export function inviteTokenFor(sub: string): string | null {
  const token = readInviteToken()
  if (token === null) return null
  const holder = sessionStorage.getItem(HOLDER_KEY)
  if (holder === null) sessionStorage.setItem(HOLDER_KEY, sub)
  else if (holder !== sub) {
    forgetInviteToken()
    return null
  }
  return token
}

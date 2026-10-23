import type { Identity } from '../auth/actors'
import type { MockRoute } from './mock-route'

export const bffPaths = {
  user: '/bff/user',
  login: '/bff/login',
  logout: '/bff/logout',
}

export const bffApi = {
  /** The claims of the signed-in user, or a 401 for an anonymous visitor, as the BFF answers. */
  userIs(identity: Identity): MockRoute {
    return identity.signedIn
      ? { method: 'get', path: bffPaths.user, status: 200, body: identity.claims }
      : { method: 'get', path: bffPaths.user, status: 401 }
  },

  /** The SPA navigates to the login of the BFF. The mock answers with an empty page, not with the redirect to the IdP. */
  loginOpens(): MockRoute {
    return { method: 'get', path: bffPaths.login, status: 200 }
  },

  /** The SPA navigates to the logout of the BFF. The mock answers with an empty page, not with the redirect to the IdP. */
  logoutOpens(): MockRoute {
    return { method: 'get', path: bffPaths.logout, status: 200 }
  },
}

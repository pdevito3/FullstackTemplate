import type { ProblemDetails } from '@/lib/problem-details'
import type { Identity } from '../auth/actors'
import type { MockRoute } from './mock-route'

export const bffPaths = {
  user: '/bff/user',
  login: '/bff/login',
  logout: '/bff/logout',
}

export const bffApi = {
  /** The claims of the signed-in user, or a 204 with no body for an anonymous visitor, as the BFF answers. */
  userIs(identity: Identity): MockRoute {
    return identity.signedIn
      ? { method: 'get', path: bffPaths.user, status: 200, body: identity.claims }
      : { method: 'get', path: bffPaths.user, status: 204 }
  },

  /** The BFF fails to read the session. */
  userFails(problem: { build(): ProblemDetails }): MockRoute {
    const body = problem.build()
    return { method: 'get', path: bffPaths.user, status: body.status ?? 500, body }
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

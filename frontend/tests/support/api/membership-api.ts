import type { InitiateUserAndTenantResponseDto } from '@/api/generated/models/types/initiate-user-and-tenant-response-dto'
import type { UserDto } from '@/api/generated/models/types/user-dto'
import type { Identity } from '../auth/actors'
import { actorSubject } from '../auth/actors'
import { aProblem } from '../builders/problem-details-builder'
import { aUser } from '../builders/user-builder'
import type { MockRoute } from './mock-route'

export const membershipPaths = {
  userOf: (identifier: string) => `/api/v1/users/by-identifier/${identifier}`,
  start: '/api/v1/users/initiate',
}

export const membershipApi = {
  /** No route for an anonymous actor, because the SPA does not look up a user record without a session. */
  userIs(identity: Identity): MockRoute[] {
    if (!identity.signedIn) return []
    const subject = identity.claims.find((claim) => claim.type === 'sub')?.value ?? actorSubject
    const path = membershipPaths.userOf(subject)
    return identity.hasTenant
      ? [{ method: 'get', path, status: 200, body: aUser().identifiedBy(subject).build() }]
      : [{ method: 'get', path, status: 404, body: { ...aProblem().saying('User was not found.').build(), status: 404 } }]
  },

  /** The caller has a user record from now on, as after a claim or a tenant start. */
  userIsNow(user: { build(): UserDto }): MockRoute {
    return { method: 'get', path: membershipPaths.userOf(actorSubject), status: 200, body: user.build() }
  },

  lookupRefuses(detail: string): MockRoute {
    const body = { ...aProblem().saying(detail).build(), status: 403 }
    return { method: 'get', path: membershipPaths.userOf(actorSubject), status: 403, body }
  },

  startReturns(response: InitiateUserAndTenantResponseDto): MockRoute {
    return { method: 'post', path: membershipPaths.start, status: 201, body: response }
  },

  /** The Server refuses one field, as it refuses a tenant name in use with a 409. */
  startRefuses(field: string, message: string): MockRoute {
    const body = { ...aProblem().saying(message).build(), status: 409, errors: { [field]: [message] } }
    return { method: 'post', path: membershipPaths.start, status: 409, body }
  },
}

import type { InitiateUserAndTenantResponseDto } from '@/api/generated/models/types/initiate-user-and-tenant-response-dto'
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
  /** The user record of a signed-in actor, or the 404 of the Server for an actor with no record. */
  userIs(identity: Identity): MockRoute[] {
    if (!identity.signedIn) return []
    const path = membershipPaths.userOf(actorSubject)
    return identity.hasTenant
      ? [{ method: 'get', path, status: 200, body: aUser().identifiedBy(actorSubject).build() }]
      : [{ method: 'get', path, status: 404, body: { ...aProblem().saying('User was not found.').build(), status: 404 } }]
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

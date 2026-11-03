import type { CurrentUserDto } from '@/api/generated/models/types/current-user-dto'
import type { ProblemDetails } from '@/lib/problem-details'
import { actorSubject, type Identity } from '../auth/actors'
import type { MockRoute } from './mock-route'

export const usersPaths = {
  current: '/api/v1/users/me',
}

export const usersApi = {
  currentOf(identity: Identity): MockRoute[] {
    if (!identity.signedIn) return []
    const name = identity.claims.find((claim) => claim.type === 'name')?.value ?? null
    return [usersApi.currentIs({ identifier: actorSubject, name })]
  },

  currentIs(user: CurrentUserDto): MockRoute {
    return { method: 'get', path: usersPaths.current, status: 200, body: user }
  },

  currentFails(problem: { build(): ProblemDetails }): MockRoute {
    const body = problem.build()
    return { method: 'get', path: usersPaths.current, status: body.status ?? 500, body }
  },
}

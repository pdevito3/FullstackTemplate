import type { CurrentUserDto } from '@/api/generated/models/types/current-user-dto'
import type { UserDto } from '@/api/generated/models/types/user-dto'
import type { ProblemDetails } from '@/lib/problem-details'
import { actorSubject, type Identity } from '../auth/actors'
import type { MockRoute } from './mock-route'
import { paginationHeaders } from './pagination'

interface Buildable<T> {
  build(): T
}

export const usersPaths = {
  current: '/api/v1/users/me',
  list: '/api/v1/users',
  one: (id: string) => `/api/v1/users/${id}`,
  role: (id: string) => `/api/v1/users/${id}/role`,
}

const problemAt = (method: MockRoute['method'], path: string, problem: Buildable<ProblemDetails>): MockRoute => {
  const body = problem.build()
  return { method, path, status: body.status ?? 500, body }
}

export const usersApi = {
  currentOf(identity: Identity): MockRoute[] {
    if (!identity.signedIn) return []
    const name = identity.claims.find((claim) => claim.type === 'name')?.value ?? null
    return [usersApi.currentIs({ identifier: actorSubject, name })]
  },

  /** The current user. The user is not an operator of the jobs dashboard unless the test says so. */
  currentIs({ canOpenJobDashboard = false, ...user }: Omit<CurrentUserDto, 'canOpenJobDashboard'> & Partial<Pick<CurrentUserDto, 'canOpenJobDashboard'>>): MockRoute {
    const body: CurrentUserDto = { ...user, canOpenJobDashboard }
    return { method: 'get', path: usersPaths.current, status: 200, body }
  },

  currentFails(problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('get', usersPaths.current, problem)
  },

  listReturns(users: Buildable<UserDto>[], { total = users.length, pageNumber = 1 } = {}): MockRoute {
    return {
      method: 'get',
      path: usersPaths.list,
      status: 200,
      headers: paginationHeaders(total, pageNumber),
      body: users.map((u) => u.build()),
    }
  },

  listFails(problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('get', usersPaths.list, problem)
  },

  /** An answer with no body, as the BFF gives when it cannot reach the Server. */
  listFailsWithNoProblem(status: number): MockRoute {
    return { method: 'get', path: usersPaths.list, status }
  },

  oneFails(id: string, problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('get', usersPaths.one(id), problem)
  },

  oneIs(user: Buildable<UserDto>): MockRoute {
    const body = user.build()
    return { method: 'get', path: usersPaths.one(body.id), status: 200, body }
  },

  roleChanges(user: Buildable<UserDto>): MockRoute {
    const body = user.build()
    return { method: 'put', path: usersPaths.role(body.id), status: 200, body }
  },

  roleChangeFails(id: string, problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('put', usersPaths.role(id), problem)
  },

  deletes(id: string): MockRoute {
    return { method: 'delete', path: usersPaths.one(id), status: 204 }
  },

  deleteFails(id: string, problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('delete', usersPaths.one(id), problem)
  },
}

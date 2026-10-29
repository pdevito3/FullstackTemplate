import type { CurrentUserDto } from '@/api/generated/models/types/current-user-dto'
import type { ProblemDetails } from '@/lib/problem-details'
import type { MockRoute } from './mock-route'

export const usersPaths = {
  current: '/api/v1/users/me',
}

export const usersApi = {
  currentIs(user: CurrentUserDto): MockRoute {
    return { method: 'get', path: usersPaths.current, status: 200, body: user }
  },

  currentFails(problem: { build(): ProblemDetails }): MockRoute {
    const body = problem.build()
    return { method: 'get', path: usersPaths.current, status: body.status ?? 500, body }
  },
}

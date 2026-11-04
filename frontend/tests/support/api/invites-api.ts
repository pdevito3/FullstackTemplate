import type { InviteDto } from '@/api/generated/models/types/invite-dto'
import type { InvitePreviewDto } from '@/api/generated/models/types/invite-preview-dto'
import type { UserDto } from '@/api/generated/models/types/user-dto'
import type { ProblemDetails } from '@/lib/problem-details'
import type { MockRoute } from './mock-route'
import { paginationHeaders } from './pagination'

interface Buildable<T> {
  build(): T
}

export const invitesPaths = {
  list: '/api/v1/invites',
  revoke: (id: string) => `/api/v1/invites/${id}/revoke`,
  preview: '/api/v1/invites/preview',
  claim: '/api/v1/invites/claim',
}

const problemAt = (method: MockRoute['method'], path: string, problem: Buildable<ProblemDetails>): MockRoute => {
  const body = problem.build()
  return { method, path, status: body.status ?? 500, body }
}

export const invitesApi = {
  listReturns(invites: Buildable<InviteDto>[], { total = invites.length, pageNumber = 1 } = {}): MockRoute {
    return {
      method: 'get',
      path: invitesPaths.list,
      status: 200,
      headers: paginationHeaders(total, pageNumber),
      body: invites.map((i) => i.build()),
    }
  },

  listFails(problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('get', invitesPaths.list, problem)
  },

  creates(invite: Buildable<InviteDto>, token: string): MockRoute {
    return { method: 'post', path: invitesPaths.list, status: 201, body: { invite: invite.build(), token } }
  },

  createFails(problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('post', invitesPaths.list, problem)
  },

  revokes(invite: Buildable<InviteDto>): MockRoute {
    const body = invite.build()
    return { method: 'post', path: invitesPaths.revoke(body.id), status: 200, body }
  },

  revokeFails(id: string, problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('post', invitesPaths.revoke(id), problem)
  },

  previewIs(preview: InvitePreviewDto): MockRoute {
    return { method: 'post', path: invitesPaths.preview, status: 200, body: preview }
  },

  previewFails(problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('post', invitesPaths.preview, problem)
  },

  claims(user: Buildable<UserDto>): MockRoute {
    return { method: 'post', path: invitesPaths.claim, status: 201, body: user.build() }
  },

  claimFails(problem: Buildable<ProblemDetails>): MockRoute {
    return problemAt('post', invitesPaths.claim, problem)
  },
}

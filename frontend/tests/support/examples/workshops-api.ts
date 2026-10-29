import type { VenueDto } from '@/api/generated/models/types/venue-dto'
import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import type { ProblemDetails } from '@/lib/problem-details'
import type { MockRoute } from '@tests/support/api/mock-route'

interface Buildable<T> {
  build(): T
}

export const workshopPaths = {
  list: '/api/v1/workshops',
  one: (id: string) => `/api/v1/workshops/${id}`,
  action: (id: string, action: 'schedule' | 'cancel' | 'complete') => `/api/v1/workshops/${id}/${action}`,
  venues: '/api/v1/venues',
  venue: (id: string) => `/api/v1/venues/${id}`,
}

/** The X-Pagination header of one page of a list, as the Server sends it. */
export function paginationOf(total: number, pageNumber = 1, pageSize = 10) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  return {
    'X-Pagination': JSON.stringify({
      totalCount: total,
      pageSize,
      currentPageSize: Math.max(0, Math.min(pageSize, total - (pageNumber - 1) * pageSize)),
      currentStartIndex: (pageNumber - 1) * pageSize + 1,
      currentEndIndex: Math.min(total, pageNumber * pageSize),
      pageNumber,
      totalPages,
      hasPrevious: pageNumber > 1,
      hasNext: pageNumber < totalPages,
    }),
  }
}

export const workshopsApi = {
  /** One page of the list. `total` is the count of all matches, so the pager has more pages when it is larger. */
  listReturns(workshops: Buildable<WorkshopDto>[], { total = workshops.length, pageNumber = 1 } = {}): MockRoute {
    return {
      method: 'get',
      path: workshopPaths.list,
      status: 200,
      headers: paginationOf(total, pageNumber),
      body: workshops.map((w) => w.build()),
    }
  },

  oneIs(workshop: Buildable<WorkshopDto>): MockRoute {
    const body = workshop.build()
    return { method: 'get', path: workshopPaths.one(body.id), status: 200, body }
  },

  createSaves(workshop: Buildable<WorkshopDto>): MockRoute {
    return { method: 'post', path: workshopPaths.list, status: 201, body: workshop.build() }
  },

  updateSaves(workshop: Buildable<WorkshopDto>): MockRoute {
    const body = workshop.build()
    return { method: 'put', path: workshopPaths.one(body.id), status: 200, body }
  },

  updateFails(id: string, problem: Buildable<ProblemDetails>): MockRoute {
    const body = problem.build()
    return { method: 'put', path: workshopPaths.one(id), status: body.status ?? 500, body }
  },

  actionMoves(action: 'schedule' | 'cancel' | 'complete', workshop: Buildable<WorkshopDto>): MockRoute {
    const body = workshop.build()
    return { method: 'post', path: workshopPaths.action(body.id, action), status: 200, body }
  },

  venueListReturns(venues: Buildable<VenueDto>[]): MockRoute {
    return {
      method: 'get',
      path: workshopPaths.venues,
      status: 200,
      headers: paginationOf(venues.length),
      body: venues.map((v) => v.build()),
    }
  },

  venueFails(id: string, problem: Buildable<ProblemDetails>): MockRoute {
    const body = problem.build()
    return { method: 'get', path: workshopPaths.venue(id), status: body.status ?? 500, body }
  },

  venueIs(venue: Buildable<VenueDto>): MockRoute {
    const body = venue.build()
    return { method: 'get', path: workshopPaths.venue(body.id), status: 200, body }
  },
}

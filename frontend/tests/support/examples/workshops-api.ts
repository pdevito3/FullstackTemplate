import type { VenueDto } from '@/api/generated/models/types/venue-dto'
import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import type { ProblemDetails } from '@/lib/problem-details'
import type { MockRoute } from '@tests/support/api/mock-route'
import { paginationOf } from '@tests/support/api/pagination'

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

export const workshopsApi = {
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

  actionFails(id: string, action: 'schedule' | 'cancel' | 'complete', problem: Buildable<ProblemDetails>): MockRoute {
    const body = problem.build()
    return { method: 'post', path: workshopPaths.action(id, action), status: body.status ?? 500, body }
  },

  venueCreateSaves(venue: Buildable<VenueDto>): MockRoute {
    return { method: 'post', path: workshopPaths.venues, status: 201, body: venue.build() }
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

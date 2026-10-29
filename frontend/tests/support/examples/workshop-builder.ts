import type { WorkshopDto, WorkshopDtoAllowedActionsEnumKey, WorkshopDtoStatusEnumKey } from '@/api/generated/models/types/workshop-dto'

import { aVenue } from './venue-builder'

let seed = 0
const next = () => ++seed

const MANAGER_ACTIONS: Record<WorkshopDtoStatusEnumKey, WorkshopDtoAllowedActionsEnumKey[]> = {
  Draft: ['Update', 'Schedule', 'Cancel'],
  Scheduled: ['Cancel', 'Complete'],
  Cancelled: [],
  Completed: [],
}

class WorkshopBuilder {
  private readonly workshop: WorkshopDto

  private constructor(workshop: WorkshopDto) {
    this.workshop = workshop
  }

  static create() {
    const n = next()
    const venue = aVenue().build()
    return new WorkshopBuilder({
      id: `00000000-0000-4000-9000-${String(n).padStart(12, '0')}`,
      venueId: venue.id,
      title: `Workshop ${n}`,
      startsAt: '2026-06-01T15:00:00+00:00',
      venueTimeZone: venue.timeZone,
      capacity: 20,
      status: 'Draft',
      allowedActions: MANAGER_ACTIONS.Draft,
      version: 1,
    })
  }

  private with(patch: Partial<WorkshopDto>) {
    return new WorkshopBuilder({ ...this.workshop, ...patch })
  }

  titled(title: string) {
    return this.with({ title })
  }
  startingAt(startsAt: string) {
    return this.with({ startsAt })
  }
  withCapacity(capacity: number) {
    return this.with({ capacity })
  }
  at(venue: { build(): { id: string; timeZone: string } }) {
    const { id, timeZone } = venue.build()
    return this.with({ venueId: id, venueTimeZone: timeZone })
  }
  inStatus(status: WorkshopDtoStatusEnumKey) {
    return this.with({ status, allowedActions: MANAGER_ACTIONS[status] })
  }
  seenByReader() {
    return this.with({ allowedActions: [] })
  }
  savedAgain(patch: Pick<Partial<WorkshopDto>, 'title' | 'startsAt' | 'capacity'> = {}) {
    return this.with({ ...patch, version: this.workshop.version + 1 })
  }

  build(): WorkshopDto {
    return this.workshop
  }
}

export const aWorkshop = () => WorkshopBuilder.create()

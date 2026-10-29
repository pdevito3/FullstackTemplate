import type { WorkshopDto, WorkshopDtoAllowedActionsEnumKey, WorkshopDtoStatusEnumKey } from '@/api/generated/models/types/workshop-dto'

import { aVenue } from './venue-builder'

let seed = 0
const next = () => ++seed

// What the Server allows a caller with manage_workshops to send in each status. A caller without it gets none.
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
    return new WorkshopBuilder({
      id: `00000000-0000-4000-9000-${String(n).padStart(12, '0')}`,
      venueId: aVenue().build().id,
      title: `Workshop ${n}`,
      startsAt: '2026-06-01T15:00:00+00:00',
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
  /** An ISO date and time with an offset, as the Server sends it. */
  startingAt(startsAt: string) {
    return this.with({ startsAt })
  }
  withCapacity(capacity: number) {
    return this.with({ capacity })
  }
  at(venue: { build(): { id: string } }) {
    return this.with({ venueId: venue.build().id })
  }
  /** The status, with the actions that a caller with manage_workshops gets for it. */
  inStatus(status: WorkshopDtoStatusEnumKey) {
    return this.with({ status, allowedActions: MANAGER_ACTIONS[status] })
  }
  /** As a caller without manage_workshops reads it: no actions. */
  seenByReader() {
    return this.with({ allowedActions: [] })
  }
  /** The workshop after another save: the next version. */
  savedAgain(patch: Pick<Partial<WorkshopDto>, 'title' | 'startsAt' | 'capacity'> = {}) {
    return this.with({ ...patch, version: this.workshop.version + 1 })
  }

  build(): WorkshopDto {
    return this.workshop
  }
}

export const aWorkshop = () => WorkshopBuilder.create()

import type { VenueDto } from '@/api/generated/models/types/venue-dto'

let seed = 0
const next = () => ++seed

class VenueBuilder {
  private readonly venue: VenueDto

  private constructor(venue: VenueDto) {
    this.venue = venue
  }

  static create() {
    const n = next()
    return new VenueBuilder({
      id: `00000000-0000-4000-8000-${String(n).padStart(12, '0')}`,
      name: `Venue ${n}`,
      timeZone: 'America/Chicago',
      version: 1,
    })
  }

  named(name: string) {
    return new VenueBuilder({ ...this.venue, name })
  }
  inZone(timeZone: string) {
    return new VenueBuilder({ ...this.venue, timeZone })
  }

  build(): VenueDto {
    return this.venue
  }
}

export const aVenue = () => VenueBuilder.create()

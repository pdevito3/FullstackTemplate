import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import { formatInstant, parseTimeZone } from '@/lib/zoned-time'

/**
 * The start of a workshop in the time zone of its venue, with the zone, for example `Mar 14, 2026, 9:00 AM CDT`. The
 * user can be in another zone than the venue.
 */
export function formatStart({ startsAt, venueTimeZone }: Pick<WorkshopDto, 'startsAt' | 'venueTimeZone'>): string {
  return formatInstant(new Date(startsAt), parseTimeZone(venueTimeZone), {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    showZone: true,
  })
}

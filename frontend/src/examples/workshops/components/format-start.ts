import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import { formatInstant, parseTimeZone } from '@/lib/zoned-time'

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

import type { WorkshopDtoStatusEnumKey } from '@/api/generated/models/types/workshop-dto'
import { Badge } from '@/components/ui/badge'

const VARIANTS: Record<WorkshopDtoStatusEnumKey, 'secondary' | 'default' | 'destructive' | 'outline'> = {
  Draft: 'secondary',
  Scheduled: 'default',
  Cancelled: 'destructive',
  Completed: 'outline',
}

export function WorkshopStatusBadge({ status }: { status: WorkshopDtoStatusEnumKey }) {
  return <Badge variant={VARIANTS[status]}>{status}</Badge>
}

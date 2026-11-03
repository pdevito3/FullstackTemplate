import { useQuery } from '@tanstack/react-query'

import type { WorkshopAttachmentDto } from '@/api/generated/models/types/workshop-attachment-dto'
import { getWorkshopAttachments } from '@/api/generated/workshops/clients/get-workshop-attachments'

import { WorkshopKeys } from './workshop.keys'

// The list loads again this long before its URLs expire, so a link on the screen still works when the user clicks it
const REFRESH_MARGIN_MS = 60_000

/** The milliseconds until the list must load again, or `false` when the list has no URL that can expire. */
export function untilRefresh(attachments: WorkshopAttachmentDto[] | undefined, now = Date.now()): number | false {
  if (!attachments?.length) {
    return false
  }
  const earliest = Math.min(...attachments.map((a) => Date.parse(a.urlsExpireAt)))
  return Math.max(earliest - REFRESH_MARGIN_MS - now, 1_000)
}

export function useWorkshopAttachments(workshopId: string) {
  return useQuery({
    queryKey: WorkshopKeys.attachments(workshopId),
    queryFn: ({ signal }) => getWorkshopAttachments({ path: { id: workshopId }, signal }),
    // The interval loads the list on an open tab. After the computer sleeps, the focus loads it, because it is stale.
    staleTime: (query) => untilRefresh(query.state.data) || 0,
    refetchInterval: (query) => untilRefresh(query.state.data),
    refetchOnWindowFocus: true,
  })
}

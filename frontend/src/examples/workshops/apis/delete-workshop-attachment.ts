import { useMutation, useQueryClient } from '@tanstack/react-query'

import { deleteWorkshopAttachment } from '@/api/generated/workshops/clients/delete-workshop-attachment'

import { WorkshopKeys } from './workshop.keys'

export function useDeleteWorkshopAttachment(workshopId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (attachmentId: string) =>
      deleteWorkshopAttachment({ path: { id: workshopId, attachmentId } }),
    // A refused delete also loads the list again, because another user can have deleted the file
    onSettled: () => queryClient.invalidateQueries({ queryKey: WorkshopKeys.attachments(workshopId) }),
  })
}

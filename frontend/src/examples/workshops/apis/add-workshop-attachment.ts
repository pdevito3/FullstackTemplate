import { useMutation, useQueryClient } from '@tanstack/react-query'

import { addWorkshopAttachment } from '@/api/generated/workshops/clients/add-workshop-attachment'

import { WorkshopKeys } from './workshop.keys'

export function useAddWorkshopAttachment(workshopId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (file: File) => addWorkshopAttachment({ path: { id: workshopId }, body: { file } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WorkshopKeys.attachments(workshopId) }),
  })
}

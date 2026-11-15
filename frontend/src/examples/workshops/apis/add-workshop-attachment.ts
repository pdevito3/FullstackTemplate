import { useMutation, useQueryClient } from '@tanstack/react-query'

import { addWorkshopAttachment } from '@/api/generated/workshops/clients/add-workshop-attachment'

import type { Upload } from '@/components/attachments/types'

import { WorkshopKeys } from './workshop.keys'

export function useAddWorkshopAttachment(workshopId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ file, signal }: Upload) => addWorkshopAttachment({ path: { id: workshopId }, body: { file }, signal }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WorkshopKeys.attachments(workshopId) }),
  })
}

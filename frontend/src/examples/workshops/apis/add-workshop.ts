import { useMutation, useQueryClient } from '@tanstack/react-query'

import type { WorkshopForCreationDto } from '@/api/generated/models/types/workshop-for-creation-dto'
import { addWorkshop } from '@/api/generated/workshops/clients/add-workshop'

import { WorkshopKeys } from './workshop.keys'

export function useAddWorkshop() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: WorkshopForCreationDto) => addWorkshop({ body, options: { suppressErrorToast: true } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WorkshopKeys.lists() }),
  })
}

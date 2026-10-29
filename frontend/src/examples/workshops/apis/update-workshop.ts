import { useMutation, useQueryClient } from '@tanstack/react-query'

import type { WorkshopForUpdateDto } from '@/api/generated/models/types/workshop-for-update-dto'
import { updateWorkshop } from '@/api/generated/workshops/clients/update-workshop'

import { WorkshopKeys } from './workshop.keys'

export function useUpdateWorkshop(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: WorkshopForUpdateDto) =>
      updateWorkshop({ path: { id }, body, options: { suppressErrorToast: true } }),
    onMutate: () => queryClient.cancelQueries({ queryKey: WorkshopKeys.detail(id) }),
    onSuccess: (workshop) => {
      queryClient.setQueryData(WorkshopKeys.detail(id), workshop)
      return queryClient.invalidateQueries({ queryKey: WorkshopKeys.lists() })
    },
  })
}

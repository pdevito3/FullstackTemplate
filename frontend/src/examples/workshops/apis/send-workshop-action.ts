import { useMutation, useQueryClient } from '@tanstack/react-query'

import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import { cancelWorkshop } from '@/api/generated/workshops/clients/cancel-workshop'
import { completeWorkshop } from '@/api/generated/workshops/clients/complete-workshop'
import { scheduleWorkshop } from '@/api/generated/workshops/clients/schedule-workshop'

import type { WorkshopCommand } from '../components/workshop-actions'
import { WorkshopKeys } from './workshop.keys'

const SEND: Record<WorkshopCommand, (id: string) => Promise<WorkshopDto>> = {
  Schedule: (id) => scheduleWorkshop({ path: { id } }),
  Cancel: (id) => cancelWorkshop({ path: { id } }),
  Complete: (id) => completeWorkshop({ path: { id } }),
}

export function useSendWorkshopAction(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (action: WorkshopCommand) => SEND[action](id),
    onSuccess: (workshop) => {
      queryClient.setQueryData(WorkshopKeys.detail(id), workshop)
      return queryClient.invalidateQueries({ queryKey: WorkshopKeys.lists() })
    },
    onError: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: WorkshopKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: WorkshopKeys.lists() }),
      ]),
  })
}

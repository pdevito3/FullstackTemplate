import { useMutation, useQueryClient } from '@tanstack/react-query'

import { addInvite } from '@/api/generated/invites/clients/add-invite'
import type { InviteForCreationDto } from '@/api/generated/models/types/invite-for-creation-dto'

import { InviteKeys } from './invite.keys'

export function useAddInvite() {
  const queryClient = useQueryClient()

  return useMutation({
    // The form shows each refusal, so a toast would say it twice.
    mutationFn: (body: InviteForCreationDto) => addInvite({ body, options: { suppressErrorToast: true } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: InviteKeys.lists() }),
    // The answer has the token, so the cache drops it when the dialog closes (INV-2).
    gcTime: 0,
  })
}

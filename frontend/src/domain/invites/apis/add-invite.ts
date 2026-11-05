import { useMutation, useQueryClient } from '@tanstack/react-query'

import { addInvite } from '@/api/generated/invites/clients/add-invite'
import type { InviteForCreationDto } from '@/api/generated/models/types/invite-for-creation-dto'

import { InviteKeys } from './invite.keys'

export function useAddInvite() {
  const queryClient = useQueryClient()

  return useMutation({
    // The form shows each refusal, so a toast would say it twice.
    mutationFn: (body: InviteForCreationDto) => addInvite({ body, options: { suppressErrorToast: true } }),
    // The promise does not go back to the mutation, because the link must not wait for the list.
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: InviteKeys.lists() }),
    // The answer has the token, so the cache drops it when the `reset` of a close removes the observer (INV-2).
    gcTime: 0,
  })
}

import { useMutation, useQueryClient } from '@tanstack/react-query'

import { revokeInvite } from '@/api/generated/invites/clients/revoke-invite'

import { InviteKeys } from './invite.keys'

export function useRevokeInvite() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => revokeInvite({ path: { id } }),
    // A refusal can come from a claim that came first, so the list reads the invite again in each case.
    onSettled: () => queryClient.invalidateQueries({ queryKey: InviteKeys.lists() }),
  })
}

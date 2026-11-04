import { useQuery } from '@tanstack/react-query'

import { getInvitePreview } from '@/api/generated/invites/clients/get-invite-preview'
import { isRefusal } from '@/domain/membership/apis/get-membership'

import { readInviteToken } from '../invite-link'
import { InviteKeys } from './invite.keys'

export function useInvitePreview({ enabled }: { enabled: boolean }) {
  return useQuery({
    queryKey: InviteKeys.preview(),
    // The page shows the refusal itself, so a toast would say it twice. Only the body has the token (INV-3).
    queryFn: ({ signal }) =>
      getInvitePreview({ body: { token: readInviteToken() ?? '' }, signal, options: { suppressErrorToast: true } }),
    enabled,
    retry: (failureCount, error) => !isRefusal(error) && failureCount < 1,
  })
}

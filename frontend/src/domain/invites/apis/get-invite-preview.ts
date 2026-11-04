import { useQuery } from '@tanstack/react-query'

import { getInvitePreview } from '@/api/generated/invites/clients/get-invite-preview'
import { isRefusal } from '@/domain/membership/apis/get-membership'

import { forgetInviteToken, readInviteToken } from '../invite-link'
import { InviteKeys } from './invite.keys'

/** A 404 or a 410 means that the link is dead for each person, so the tab must not keep it. */
const linkIsDead = (error: unknown) => isRefusal(error) && (error.status === 404 || error.status === 410)

export function useInvitePreview({ enabled }: { enabled: boolean }) {
  return useQuery({
    queryKey: InviteKeys.preview(),
    queryFn: async ({ signal }) => {
      try {
        // The page shows the refusal itself, so a toast would say it twice. Only the body has the token (INV-3).
        return await getInvitePreview({ body: { token: readInviteToken() ?? '' }, signal, options: { suppressErrorToast: true } })
      } catch (error) {
        if (linkIsDead(error)) forgetInviteToken()
        throw error
      }
    },
    // A forgotten token leaves nothing to send, and a read on a reconnect must not send an empty token.
    enabled: enabled && readInviteToken() !== null,
    retry: (failureCount, error) => !isRefusal(error) && failureCount < 1,
  })
}

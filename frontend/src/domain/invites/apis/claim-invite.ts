import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query'

import { claimInvite } from '@/api/generated/invites/clients/claim-invite'
import type { ClaimInviteDto } from '@/api/generated/models/types/claim-invite-dto'
import { getTenant } from '@/api/generated/tenants/clients/get-tenant'
import { callerRecordQuery } from '@/domain/membership/apis/get-membership'
import { refreshCallerMembership } from '@/domain/membership/apis/refresh-caller-membership'
import { isProblemDetailsError } from '@/lib/problem-details'

import { forgetInviteToken, readInviteToken } from '../invite-link'

export type ClaimValues = Omit<ClaimInviteDto, 'token'>

interface Claim {
  values: ClaimValues
  identifier: string
  tenantName: string
}

/** A 409 or a 410 after the own submit can mean that an earlier submit of this user won. */
const canBeAnEarlierWin = (error: unknown) =>
  isProblemDetailsError(error) && (error.status === 409 || error.status === 410)

/** The preview names the tenant only by its name, and the name of a tenant is unique. */
async function joined(queryClient: QueryClient, { identifier, tenantName }: Claim): Promise<boolean> {
  try {
    const user = await queryClient.fetchQuery(callerRecordQuery(identifier))
    if (!user) return false
    const tenant = await getTenant({ path: { id: user.tenantId }, options: { suppressErrorToast: true } })
    return tenant.name === tenantName
  } catch {
    return false
  }
}

export function useClaimInvite() {
  const queryClient = useQueryClient()

  return useMutation({
    // The token is not a variable of the mutation, so the mutation cache does not hold it.
    mutationFn: async (claim: Claim) => {
      const body = { ...claim.values, token: readInviteToken() ?? '' }
      try {
        await claimInvite({ body, options: { suppressErrorToast: true } })
      } catch (error) {
        if (!canBeAnEarlierWin(error) || !(await joined(queryClient, claim))) throw error
      }
    },
    onSuccess: async () => {
      forgetInviteToken()
      await refreshCallerMembership(queryClient)
    },
  })
}

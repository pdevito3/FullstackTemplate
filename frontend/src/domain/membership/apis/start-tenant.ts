import { useMutation, useQueryClient } from '@tanstack/react-query'

import type { InitiateUserAndTenantDto } from '@/api/generated/models/types/initiate-user-and-tenant-dto'
import { initiateUserAndTenant } from '@/api/generated/users/clients/initiate-user-and-tenant'

import { MembershipKeys } from './membership.keys'

export function useStartTenant() {
  const queryClient = useQueryClient()

  return useMutation({
    // The form shows each refusal next to its field, so a toast would say it twice.
    mutationFn: (body: InitiateUserAndTenantDto) =>
      initiateUserAndTenant({ body, options: { suppressErrorToast: true } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: MembershipKeys.all }),
  })
}

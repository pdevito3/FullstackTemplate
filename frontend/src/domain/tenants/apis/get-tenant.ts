import { useQuery } from '@tanstack/react-query'

import { getTenant } from '@/api/generated/tenants/clients/get-tenant'

import { TenantKeys } from './tenant.keys'

export function useTenant(id: string | undefined) {
  return useQuery({
    queryKey: TenantKeys.detail(id ?? ''),
    queryFn: ({ signal }) => getTenant({ path: { id: id! }, signal }),
    enabled: !!id,
    // `allowedActions` depends on the permissions of the caller, and a role change can come from another page. So a
    // page that comes back waits for the new answer, and does not show a button of the old role.
    gcTime: 0,
  })
}

import type { QueryClient } from '@tanstack/react-query'

import { MembershipKeys } from './membership.keys'

export const refreshCallerMembership = (queryClient: QueryClient) =>
  queryClient.invalidateQueries({ queryKey: MembershipKeys.all })

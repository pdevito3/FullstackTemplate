import type { QueryClient } from '@tanstack/react-query'

import { MembershipKeys } from './membership.keys'

export const refreshCallerMembership = (queryClient: QueryClient) =>
  queryClient.invalidateQueries({ queryKey: MembershipKeys.all })

export const retryFailedMembership = (queryClient: QueryClient) =>
  queryClient.refetchQueries(
    { queryKey: MembershipKeys.all, predicate: (query) => query.state.status === 'error' },
    { cancelRefetch: false },
  )

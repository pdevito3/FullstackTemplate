import { useQuery } from '@tanstack/react-query'

import { getCurrentUser } from '@/api/generated/users/clients/get-current-user'

import { UserKeys } from './user.keys'

export function useCurrentUser() {
  return useQuery({
    queryKey: UserKeys.current(),
    queryFn: ({ signal }) => getCurrentUser({ signal }),
  })
}

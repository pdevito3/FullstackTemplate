import { useQuery } from '@tanstack/react-query'

import { getCurrentUser } from '@/api/generated/users/clients/get-current-user'

import { UserKeys } from './user.keys'

/** The signed-in user as the Server reads it from the access token. Call it only for a signed-in user. */
export function useCurrentUser() {
  return useQuery({
    queryKey: UserKeys.current(),
    queryFn: ({ signal }) => getCurrentUser({ signal }),
  })
}

import { queryOptions, useQuery } from '@tanstack/react-query'

import { getUser } from '@/api/generated/users/clients/get-user'

import { UserKeys } from './user.keys'

export const userQueryOptions = (id: string) =>
  queryOptions({
    queryKey: UserKeys.detail(id),
    queryFn: ({ signal }) => getUser({ path: { id }, signal }),
  })

export function useUser(id: string, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({ ...userQueryOptions(id), enabled })
}

import { queryOptions, useQuery } from '@tanstack/react-query'

import { getUser } from '@/api/generated/users/clients/get-user'

import { UserKeys } from './user.keys'

export const userQueryOptions = (id: string) =>
  queryOptions({
    queryKey: UserKeys.detail(id),
    // The page of the user shows each error in its alert, so the error shows no toast (messages.md, "In the SPA").
    queryFn: ({ signal }) => getUser({ path: { id }, signal, options: { suppressErrorToast: true } }),
  })

export function useUser(id: string, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({ ...userQueryOptions(id), enabled })
}

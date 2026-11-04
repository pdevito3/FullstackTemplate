import { keepPreviousData, useQuery } from '@tanstack/react-query'

import { getUserList } from '@/api/generated/users/clients/get-user-list'
import { parsePagination } from '@/lib/pagination'

import { UserKeys } from './user.keys'

export const USER_PAGE_SIZE = 10

export interface UserListParams {
  page: number
}

export function useUserList(params: UserListParams, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    enabled,
    queryKey: UserKeys.list(params),
    queryFn: async ({ signal }) => {
      const result = await getUserList({ query: { pageNumber: params.page, pageSize: USER_PAGE_SIZE }, signal })
      return { users: result.data, pagination: parsePagination(result) }
    },
    placeholderData: keepPreviousData,
  })
}

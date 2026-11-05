import { keepPreviousData, useQuery } from '@tanstack/react-query'

import { getInviteList } from '@/api/generated/invites/clients/get-invite-list'
import { parsePagination } from '@/lib/pagination'

import { InviteKeys, type InviteListParams } from './invite.keys'

export const INVITE_PAGE_SIZE = 10

export function useInviteList(params: InviteListParams) {
  return useQuery({
    queryKey: InviteKeys.list(params),
    queryFn: async ({ signal }) => {
      const result = await getInviteList({ query: { pageNumber: params.page, pageSize: INVITE_PAGE_SIZE }, signal })
      return { invites: result.data, pagination: parsePagination(result) }
    },
    placeholderData: keepPreviousData,
  })
}

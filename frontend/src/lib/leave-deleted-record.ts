import type { QueryClient, QueryKey } from '@tanstack/react-query'

interface DeletedRecord {
  /** The detail key of the deleted record, such as `RoomKeys.detail(id)`. */
  detail: QueryKey
  /** The lists that showed the record, such as `RoomKeys.lists()`. */
  lists: QueryKey
  /** Goes to the page after the delete, such as `() => navigate({ to: '/rooms' })`. */
  leave: () => Promise<unknown>
}

export async function leaveDeletedRecord(queryClient: QueryClient, { detail, lists, leave }: DeletedRecord) {
  queryClient.removeQueries({ queryKey: detail })
  await queryClient.invalidateQueries({ queryKey: lists })
  await leave()
}

import type { QueryClient, QueryKey } from '@tanstack/react-query'

interface DeletedRecord {
  detail: QueryKey
  /** The lists that showed the record, such as `RoomKeys.lists()`. */
  lists: QueryKey
  /** Goes to the page after the delete, such as `() => navigate({ to: '/rooms' })`. */
  leave: () => Promise<unknown>
}

/**
 * After a delete, call this in the `onSuccess` of `mutate`. It marks the lists stale while no list is on the screen,
 * so the next list reads the Server once. It leaves the page first and drops the detail after: a query that is on the
 * screen reads a dropped key again at the next render, and that read gets a 404 next to the toast of the delete.
 */
export async function leaveDeletedRecord(queryClient: QueryClient, { detail, lists, leave }: DeletedRecord) {
  await queryClient.invalidateQueries({ queryKey: lists })
  await leave()
  queryClient.removeQueries({ queryKey: detail })
}

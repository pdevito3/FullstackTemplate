// The list loads again this long before its URLs expire, so a link on the screen still works when the user clicks it
const REFRESH_MARGIN_MS = 60_000

interface ExpiringUrls {
  urlsExpireAt: string
}

interface AttachmentListQuery {
  state: { data?: { attachments: readonly ExpiringUrls[] }; dataUpdatedAt: number }
}

/** The time from `from` until the list must load again, or false when the list has no URL that expires. */
export function untilRefresh(attachments: readonly ExpiringUrls[] | undefined, from: number): number | false {
  if (!attachments?.length) {
    return false
  }
  const earliest = Math.min(...attachments.map((a) => Date.parse(a.urlsExpireAt)))
  return Math.max(earliest - REFRESH_MARGIN_MS - from, 1_000)
}

/**
 * The cache options of a list of attachments, so that each presigned URL on the screen works. Spread them into the
 * `queryOptions` of the list. TanStack Query adds the stale time to the time of the load, but it starts the refetch
 * interval at each subscribe and each update. So the stale time counts from the load, and the interval counts from
 * now. The interval loads the list on an open tab. After the computer sleeps, the focus loads it, because it is stale.
 */
export const refreshBeforeUrlsExpire = {
  staleTime: (query: AttachmentListQuery): number => untilRefresh(query.state.data?.attachments, query.state.dataUpdatedAt) || 0,
  refetchInterval: (query: AttachmentListQuery): number | false => untilRefresh(query.state.data?.attachments, Date.now()),
  refetchOnWindowFocus: true,
}

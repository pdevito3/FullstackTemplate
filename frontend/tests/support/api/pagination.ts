export function paginationHeaders(total: number, pageNumber = 1, pageSize = 10) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  return {
    'X-Pagination': JSON.stringify({
      totalCount: total,
      pageSize,
      currentPageSize: Math.max(0, Math.min(pageSize, total - (pageNumber - 1) * pageSize)),
      currentStartIndex: (pageNumber - 1) * pageSize + 1,
      currentEndIndex: Math.min(total, pageNumber * pageSize),
      pageNumber,
      totalPages,
      hasPrevious: pageNumber > 1,
      hasNext: pageNumber < totalPages,
    }),
  }
}

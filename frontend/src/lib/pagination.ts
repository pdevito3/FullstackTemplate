import type { AxiosResponse } from 'axios'

import type { PaginationMetadata } from '@/api/generated/models/types/pagination-metadata'

const paginationHeader = 'x-pagination'

/**
 * Reads the pagination data from the X-Pagination header of a list response.
 * A list client (an operation name that ends in `List`) returns the full result, so pass that result here.
 *
 * @example
 * ```typescript
 * const result = await getTenantList({ query: { PageNumber: 1, PageSize: 10 }, signal })
 * const tenants = result.data
 * const pagination = parsePagination(result)
 * ```
 */
export function parsePagination(result: { response: Pick<AxiosResponse, 'headers'> }): PaginationMetadata {
  const header: unknown = result.response.headers[paginationHeader]
  if (typeof header !== 'string') {
    throw new Error(`The response has no ${paginationHeader} header.`)
  }
  return JSON.parse(header) as PaginationMetadata
}

import type { AxiosResponse } from 'axios'

import type { PaginationMetadata } from '@/api/generated/models/types/pagination-metadata'
import { paginationMetadataSchema } from '@/api/generated/models/zod/pagination-metadata'

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
  // A header name is case-insensitive, but a property lookup on the headers is not.
  const header: unknown = Object.entries(result.response.headers).find(
    ([name]) => name.toLowerCase() === paginationHeader,
  )?.[1]
  if (typeof header !== 'string') {
    throw new Error(`The response has no ${paginationHeader} header.`)
  }
  // The schema rejects a header that does not have the shape of the contract, so a caller does not read a wrong type.
  return paginationMetadataSchema.parse(JSON.parse(header))
}

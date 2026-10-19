import { AxiosHeaders } from 'axios'
import { describe, expect, it } from 'vitest'
import { ZodError } from 'zod'

import type { PaginationMetadata } from '@/api/generated/models/types/pagination-metadata'
import { parsePagination } from '@/lib/pagination'

const metadata: PaginationMetadata = {
  totalCount: 42,
  pageSize: 10,
  currentPageSize: 10,
  currentStartIndex: 11,
  currentEndIndex: 20,
  pageNumber: 2,
  totalPages: 5,
  hasPrevious: true,
  hasNext: true,
}

function resultWith(headers: AxiosHeaders) {
  return { response: { headers } }
}

describe('parsePagination', () => {
  it('reads a valid X-Pagination header', () => {
    const headers = AxiosHeaders.from(`x-pagination: ${JSON.stringify(metadata)}`)

    expect(parsePagination(resultWith(headers))).toEqual(metadata)
  })

  // The axios adapters parse the raw response headers, and that makes the names lowercase.
  it('reads the header when the server sends it in a different case', () => {
    const headers = AxiosHeaders.from(`X-Pagination: ${JSON.stringify(metadata)}`)

    expect(parsePagination(resultWith(headers))).toEqual(metadata)
  })

  it('reads the header from plain headers with a mixed-case name', () => {
    const headers = { 'X-Pagination': JSON.stringify(metadata) }

    expect(parsePagination({ response: { headers } })).toEqual(metadata)
  })

  it('reads the header from AxiosHeaders built with a mixed-case name', () => {
    const headers = new AxiosHeaders({ 'X-Pagination': JSON.stringify(metadata) })

    expect(parsePagination(resultWith(headers))).toEqual(metadata)
  })

  it('throws when the header is missing', () => {
    const headers = new AxiosHeaders()

    expect(() => parsePagination(resultWith(headers))).toThrow('The response has no x-pagination header.')
  })

  it('throws when the header does not match the contract', () => {
    const headers = AxiosHeaders.from(`x-pagination: ${JSON.stringify({ ...metadata, totalCount: '42' })}`)

    expect(() => parsePagination(resultWith(headers))).toThrow(ZodError)
  })

  it('throws when the header is not valid JSON', () => {
    const headers = AxiosHeaders.from('x-pagination: not json')

    expect(() => parsePagination(resultWith(headers))).toThrow(SyntaxError)
  })
})

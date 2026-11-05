import { describe, expect, it } from 'vitest'

import { returnUrlOf } from './auth-actions'

const at = (pathname: string, search = '', hash = '') => ({ pathname, search, hash })

describe('the return URL of the login', () => {
  it('is the path, the query, and the hash of the page', () => {
    expect(returnUrlOf(at('/about', '?tab=team', '#people'))).toBe('/about?tab=team#people')
  })

  it.each([
    ['a percent-encoded token', '#token%3Dabc'],
    ['another case', '#TOKEN=abc'],
    ['a query in the fragment', '#?token=abc'],
    ['a value after another value', '#a=1;token=abc'],
  ])('@INV-3 leaves out a hash with %s', (_, hash) => {
    expect(returnUrlOf(at('/join', '?a=1', hash))).toBe('/join?a=1')
  })

  it.each([
    ['//', '//evil.example/x'],
    ['/\\', '/\\evil.example/x'],
  ])('is / for a path that starts with %s, which the BFF refuses', (_, pathname) => {
    expect(returnUrlOf(at(pathname, '?a=1'))).toBe('/')
  })

  it('is the path of the page when the path, the query, and the hash are longer than 2048 characters', () => {
    const search = `?q=${'a'.repeat(2048)}`

    expect(returnUrlOf(at('/about', search, '#people'))).toBe('/about')
  })

  it('keeps a return URL of exactly 2048 characters', () => {
    const pathname = `/${'a'.repeat(2047)}`

    expect(returnUrlOf(at(pathname))).toBe(pathname)
  })

  it('is / when the path alone is longer than 2048 characters', () => {
    expect(returnUrlOf(at(`/${'a'.repeat(2048)}`, '?a=1'))).toBe('/')
  })
})

import { describe, expect, it } from 'vitest'

import { formatFileSize } from './format-file-size'

describe('formatFileSize', () => {
  it.each([
    [1, '1 byte'],
    [999, '999 bytes'],
    [1000, '1 kB'],
    [1536, '1.5 kB'],
    [999.9 * 1024, '999.9 kB'],
    [999.96 * 1024, '1 MB'],
    [1000 * 1024, '1 MB'],
    [1024 * 1024 - 1, '1 MB'],
    [12 * 1024 * 1024, '12 MB'],
  ])('formats %i bytes as %s', (bytes, text) => {
    expect(formatFileSize(bytes, 'en-US')).toBe(text)
  })
})

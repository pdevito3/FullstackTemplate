import { describe, expect, it } from 'vitest'

import { containsText } from '@/lib/querykit-text'

interface Row {
  title: string
}

describe('containsText', () => {
  it.each([
    ['a word', 'Paul', 'title @=* "Paul"'],
    ['a percent and an underscore', '50%_off', 'title @=* "50%_off"'],
    ['a quote at the end', 'quote"', 'title @=* """ quote" """'],
    ['a quote at the start', '"Paul', 'title @=* """ "Paul """'],
    ['a run of three quotes', 'a"""b', 'title @=* """" a"""b """"'],
    ['spaces at the ends', '  Paul ', 'title @=* "Paul"'],
  ])('puts text with %s in a filter that QueryKit reads as the text', (_, text, filter) => {
    expect(containsText<Row>('title', text)).toBe(filter)
  })

  it.each(['', '  '])('adds no filter for the blank text "%s"', (text) => {
    expect(containsText<Row>('title', text)).toBeUndefined()
  })
})

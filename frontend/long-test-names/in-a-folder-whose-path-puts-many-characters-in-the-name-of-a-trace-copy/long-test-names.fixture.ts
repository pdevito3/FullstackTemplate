import { describe, expect, test } from 'vitest'

const longName = `has a name longer than a file name can be ${'x'.repeat(260)}`

describe('a test file with long test names', () => {
  test(`fails and ${longName}`, () => {
    // Vitest takes no failure screenshot of an empty page.
    document.body.textContent = 'A failed test'
    expect(1).toBe(2)
  })

  test(`passes and ${longName}`, () => {
    expect(1).toBe(1)
  })

  test('runs after them', () => {
    expect(true).toBe(true)
  })
})

import { describe, expect, it } from 'vitest'

import { inviteLinkOf, tokenOfFragment } from './invite-link'

describe('the token in the fragment of an invite link', () => {
  it('@INV-3 is in the fragment of the link, and it reads back from that fragment', () => {
    const link = new URL(inviteLinkOf('https://app.example.com', 'abc-DEF_123'))

    expect(link.pathname).toBe('/join')
    expect(link.search).toBe('')
    expect(tokenOfFragment(link.hash)).toBe('abc-DEF_123')
  })

  it.each([
    ['no fragment', '', null],
    ['another fragment', '#people', null],
    ['an empty token', '#token=', ''],
    ['a second value', '#a=1&token=abc', 'abc'],
  ])('reads %s', (_, hash, token) => {
    expect(tokenOfFragment(hash)).toBe(token)
  })
})

import { describe, expect, it } from 'vitest'

import { formatExpiry, inviteStateOf } from './invite-state'

const expiresAt = '2026-08-16T07:00:00Z'

describe('the state of an invite', () => {
  it.each([
    ['Pending', '2026-08-16T06:59:59Z', 'Pending'],
    ['Pending', '2026-08-16T07:00:00Z', 'Expired'],
    ['Claimed', '2026-08-17T00:00:00Z', 'Claimed'],
    ['Revoked', '2026-08-17T00:00:00Z', 'Revoked'],
  ] as const)('%s at %s shows as %s', (status, now, state) => {
    expect(inviteStateOf({ status, expiresAt }, new Date(now))).toBe(state)
  })
})

describe('the expiry of an invite', () => {
  it('shows in the zone of the browser, which is Pacific/Pago_Pago in this suite', () => {
    expect(formatExpiry(expiresAt)).toMatch(/^Aug 15, 2026, 8:00\s?PM$/)
  })
})

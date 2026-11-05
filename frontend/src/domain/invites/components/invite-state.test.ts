import { describe, expect, it } from 'vitest'

import type { InviteDto } from '@/api/generated/models/types/invite-dto'

import { formatExpiry, inviteStateOf } from './invite-state'

const expiresAt = '2026-08-16T07:00:00Z'

describe('the state of an invite', () => {
  it.each([
    ['Pending', false, 'Pending'],
    ['Pending', true, 'Expired'],
    ['Claimed', false, 'Claimed'],
    ['Revoked', false, 'Revoked'],
  ] as const)('%s with the expired flag %s shows as %s', (status, isExpired, state) => {
    expect(inviteStateOf({ status, isExpired })).toBe(state)
  })

  it('reads the flag of the Server and not the clock of the browser', () => {
    const longAgo = '2020-01-15T12:00:00Z'
    const farAhead = '2099-01-15T12:00:00Z'

    expect(inviteStateOf({ status: 'Pending', isExpired: false, expiresAt: longAgo } as InviteDto)).toBe('Pending')
    expect(inviteStateOf({ status: 'Pending', isExpired: true, expiresAt: farAhead } as InviteDto)).toBe('Expired')
  })
})

describe('the expiry of an invite', () => {
  it('shows in the zone of the browser, which is Pacific/Pago_Pago in this suite', () => {
    expect(formatExpiry(expiresAt)).toMatch(/^Aug 15, 2026, 8:00\s?PM$/)
  })
})

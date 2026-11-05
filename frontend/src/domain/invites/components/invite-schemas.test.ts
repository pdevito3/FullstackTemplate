import { describe, expect, it } from 'vitest'

import { claimInviteSchema } from './claim-invite-schema'
import { inviteUserSchema } from './invite-user-schema'

const claim = { firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', username: 'ada' }

describe('the values of a claim', () => {
  it('@INV-8 needs each value', () => {
    const result = claimInviteSchema(false).safeParse({ firstName: '', lastName: '', email: '', username: '' })

    expect(result.error?.issues.map((issue) => issue.message)).toEqual([
      'Enter your first name.',
      'Enter your last name.',
      'Enter your email address.',
      'Enter a username.',
    ])
  })

  it('@INV-9 needs no email on an invite with an email match, because the Server uses the email of the token', () => {
    expect(claimInviteSchema(true).safeParse({ ...claim, email: '' }).success).toBe(true)
  })

  it.each([
    ['firstName', 200],
    ['lastName', 200],
    ['email', 320],
    ['username', 100],
  ])('holds %s to the limit of the contract, in characters', (name, limit) => {
    expect(claimInviteSchema(false).safeParse({ ...claim, [name]: '😀'.repeat(limit) }).success).toBe(true)
    expect(claimInviteSchema(false).safeParse({ ...claim, [name]: 'a'.repeat(limit + 1) }).success).toBe(false)
  })
})

describe('the values of an invite', () => {
  it('takes an empty email', () => {
    expect(inviteUserSchema.safeParse({ role: 'User', email: '', requireEmailMatch: false }).success).toBe(true)
  })

  it('leaves the rule of an email match with no email to the Server', () => {
    expect(inviteUserSchema.safeParse({ role: 'User', email: '', requireEmailMatch: true }).success).toBe(true)
  })

  it('holds the email to the limit of the contract', () => {
    expect(inviteUserSchema.safeParse({ role: 'User', email: 'a'.repeat(321), requireEmailMatch: false }).success).toBe(false)
  })
})

import { describe, expect, it } from 'vitest'

import type { Membership } from '@/domain/membership/apis/get-membership'
import { ProblemDetailsError } from '@/lib/problem-details'

import { joinViewOf, LINK_NOT_VALID } from './join-view'

const profile = { identifier: 'ada', firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', username: 'ada' }
const noTenant: Membership = { status: 'no-tenant', profile }
const member = { status: 'member', user: { id: 'usr_1' } } as Membership
const data = { tenantName: 'Acme', role: 'User', expiresAt: '2099-01-15T12:00:00Z', requireEmailMatch: false } as const
const refusal = (status: number, detail: string) =>
  new ProblemDetailsError({ type: 'about:blank', title: 'Refused', status, detail })

const facts = {
  membership: noTenant,
  linkFits: true,
  preview: { data, error: null },
  refusedLink: null,
  claim: 'idle',
} as const

describe('the view of the join page', () => {
  it('waits for the membership', () => {
    expect(joinViewOf({ ...facts, membership: { status: 'loading' } })).toEqual({ kind: 'loading' })
  })

  it('shows the failure of the membership check before it says anything about the link', () => {
    const view = joinViewOf({ ...facts, linkFits: false, membership: { status: 'error', message: 'Failed.' } })

    expect(view).toEqual({ kind: 'membership-failed', message: 'Failed.' })
  })

  it('@INV-6 tells a member that the account has a tenant, also with a link that is not good', () => {
    expect(joinViewOf({ ...facts, membership: member, linkFits: false })).toEqual({ kind: 'member' })
  })

  it('shows a member who has this claim in progress as joining', () => {
    expect(joinViewOf({ ...facts, membership: member, claim: 'pending' })).toEqual({ kind: 'joining' })
  })

  it('stays on joining after a success, also before the membership has its new answer', () => {
    expect(joinViewOf({ ...facts, claim: 'success' })).toEqual({ kind: 'joining' })
  })

  it('@INV-3 refuses a link with no token that fits, with no preview', () => {
    expect(joinViewOf({ ...facts, linkFits: false, preview: { error: null } })).toEqual({
      kind: 'link-refused',
      message: LINK_NOT_VALID,
    })
  })

  it.each([404, 410])('@INV-3 shows the message of a %i refusal of the preview', (status) => {
    const preview = { error: refusal(status, 'Ask a tenant admin for a new invite.') }

    expect(joinViewOf({ ...facts, preview })).toEqual({ kind: 'link-refused', message: 'Ask a tenant admin for a new invite.' })
  })

  it('offers a retry for a failure of the preview that is not a refusal', () => {
    const error = new Error('Failed to fetch')

    expect(joinViewOf({ ...facts, preview: { error } })).toEqual({ kind: 'preview-failed', error })
  })

  it('keeps the refusal of a claim over the data of the preview', () => {
    expect(joinViewOf({ ...facts, refusedLink: 'Gone.' })).toEqual({ kind: 'link-refused', message: 'Gone.' })
  })

  it('waits for the preview', () => {
    expect(joinViewOf({ ...facts, preview: { error: null } })).toEqual({ kind: 'loading' })
  })

  it('@INV-8 gives the form the preview and the profile', () => {
    expect(joinViewOf(facts)).toEqual({ kind: 'claimable', preview: data, profile })
  })
})

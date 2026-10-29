import { describe, expect, it } from 'vitest'

import { ProblemDetailsError, STALE_VERSION_PROBLEM_TYPE } from '@/lib/problem-details'

import { refusalOf } from './use-server-errors'

const FIELDS = ['title', 'capacity'] as const

const validation = (errors: Record<string, string[]>) =>
  new ProblemDetailsError({ type: 'validation', title: 'One or more validation errors occurred.', status: 422, errors })

describe('refusalOf', () => {
  it('puts each message of a field of the form on the field', () => {
    const refusal = refusalOf(validation({ title: ['Too long.', 'Has a tab.'], capacity: ['Too big.'] }), FIELDS)

    expect(refusal).toEqual({ byField: { title: 'Too long. Has a tab.', capacity: 'Too big.' }, problem: null })
  })

  it('shows a message of a field that the form does not have above the buttons', () => {
    const refusal = refusalOf(validation({ status: ['A cancelled workshop cannot be edited.'] }), FIELDS)

    expect(refusal.problem).toEqual({ kind: 'refused', messages: ['A cancelled workshop cannot be edited.'] })
  })

  it('makes a stale version its own problem, with no message on a field', () => {
    const stale = new ProblemDetailsError({
      type: STALE_VERSION_PROBLEM_TYPE,
      title: 'The record changed after you loaded it.',
      status: 409,
      errors: { title: ['Not a field message.'] },
    })

    expect(refusalOf(stale, FIELDS)).toEqual({ byField: {}, problem: { kind: 'stale' } })
  })

  it('shows the message of an error with no field messages, so the form always says why', () => {
    const failure = new ProblemDetailsError({ type: 'error', title: 'An error occurred.', status: 500, detail: 'The database is down.' })

    expect(refusalOf(failure, FIELDS).problem).toEqual({ kind: 'refused', messages: ['The database is down.'] })
    expect(refusalOf(new Error(''), FIELDS).problem).toEqual({ kind: 'refused', messages: ['The save failed. Try again.'] })
  })
})

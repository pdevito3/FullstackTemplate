import { describe, expect, it } from 'vitest'

import { ProblemDetailsError, isProblemDetails, isProblemDetailsError } from '@/lib/problem-details'

const problem = {
  type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
  title: 'The resource was not found.',
  status: 404,
  detail: 'Forecast was not found.',
  traceId: '00-abc-def-00',
}

describe('isProblemDetails', () => {
  it('accepts a body with a type, a title, and a status', () => {
    expect(isProblemDetails(problem)).toBe(true)
  })

  it.each([
    ['null', null],
    ['a string', 'Not Found'],
    ['a body with no status', { type: problem.type, title: problem.title }],
    ['a body with a text status', { ...problem, status: '404' }],
    ['a body with no type', { title: problem.title, status: 404 }],
  ])('rejects %s', (_, body) => {
    expect(isProblemDetails(body)).toBe(false)
  })
})

describe('ProblemDetailsError', () => {
  it('gives the members of the problem', () => {
    const error = new ProblemDetailsError(problem)

    expect(error.name).toBe('ProblemDetailsError')
    expect(error.status).toBe(404)
    expect(error.title).toBe('The resource was not found.')
    expect(error.detail).toBe('Forecast was not found.')
    expect(error.traceId).toBe('00-abc-def-00')
    expect(isProblemDetailsError(error)).toBe(true)
    expect(isProblemDetailsError(new Error('x'))).toBe(false)
  })

  it('uses the detail as the message', () => {
    const error = new ProblemDetailsError(problem)

    expect(error.message).toBe('Forecast was not found.')
    expect(error.displayMessage).toBe('Forecast was not found.')
  })

  it('uses the title when the problem has no detail', () => {
    const error = new ProblemDetailsError({ ...problem, detail: null })

    expect(error.message).toBe('The resource was not found.')
    expect(error.displayMessage).toBe('The resource was not found.')
  })

  it('uses a default display message when the problem has no detail and no title', () => {
    const error = new ProblemDetailsError({ type: problem.type, status: 500 })

    expect(error.displayMessage).toBe('An error occurred')
  })
})

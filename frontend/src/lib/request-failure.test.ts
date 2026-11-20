import { AxiosError } from 'axios'
import { describe, expect, it } from 'vitest'

import { ProblemDetailsError } from '@/lib/problem-details'
import { answerError, failureText, requestError } from '@/lib/request-failure'

const problem = (body: Record<string, unknown>) => new ProblemDetailsError({ type: 'urn:test', title: 'Title', ...body })

const serverFault = 'The server could not complete the request. Try again in a moment.'
const timedOut = 'The server did not answer in time. Reload the page to see the latest data, then try again.'
const signInRejected =
  'The server did not accept your sign-in for this request. Try again in a moment. If it fails again, log out and log in again.'
const appFault = 'The app has a fault. Reload the page, then try again.'

describe('failureText of a refusal of the Server', () => {
  it('gives the detail of a 4xx problem', () => {
    expect(failureText(problem({ status: 404, title: 'The resource was not found.', detail: 'Tenant was not found.' }))).toBe('Tenant was not found.')
  })

  it('gives the title of a 4xx problem with no detail', () => {
    expect(failureText(problem({ status: 422, title: 'One or more validation errors occurred.', detail: null }))).toBe(
      'One or more validation errors occurred.',
    )
  })

  it('gives the text of the SPA for a 4xx problem with no text', () => {
    expect(failureText(problem({ status: 403, title: '' }))).toBe(serverFault)
  })

  it('gives the sign-in text for a 401, also with a problem', () => {
    expect(failureText(problem({ status: 401, detail: 'The token expired.' }))).toBe(signInRejected)
    expect(failureText(answerError(401, ''))).toBe(signInRejected)
  })
})

describe('failureText of a failure with no text for the user', () => {
  it.each([
    ['no detail and no title', { status: 500, title: null }],
    ['the default title of ASP.NET', { status: 500, title: 'An error occurred while processing your request.' }],
    ['a detail for a developer', { status: 503, title: 'Error', detail: 'Npgsql timeout' }],
    ['no status', { status: null }],
  ])('says what happened and what to do next for a 5xx problem with %s', (_, body) => {
    expect(failureText(problem(body))).toBe(serverFault)
  })

  it.each([
    ['no body', 502, ''],
    ['a message field', 503, { message: 'Down for maintenance.' }],
    ['no body, as for a path that the BFF does not have', 404, ''],
  ])('gives the text of a fault of the Server for an answer with %s', (_, status, body) => {
    expect(failureText(answerError(status, body))).toBe(serverFault)
  })

  it('says that the server did not answer in time for a 504 with no body and for the time limit of the app', () => {
    expect(failureText(answerError(504, ''))).toBe(timedOut)
    expect(failureText(requestError(new AxiosError('timeout of 35000ms exceeded', AxiosError.ETIMEDOUT)))).toBe(timedOut)
  })

  it('says that the app could not reach the server for a request with no answer', () => {
    expect(failureText(requestError(new AxiosError('Network Error', AxiosError.ERR_NETWORK)))).toBe(
      'The app could not reach the server. Check your connection, then try again.',
    )
  })

  it.each([
    ['an error of the code', new Error('The response has no x-pagination header.')],
    ['an error with no message', new Error('')],
    ['a value that is not an error', 'boom'],
  ])('does not show the text of %s, which is for a developer', (_, error) => {
    expect(failureText(error)).toBe(appFault)
  })
})

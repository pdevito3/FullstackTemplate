import { AxiosError } from 'axios'

import { ProblemDetailsError, isProblemDetails, isProblemDetailsError, type ProblemDetails } from './problem-details'

declare module '@tanstack/react-query' {
  interface Register {
    defaultError: unknown
  }
}

type SpaTextKind = 'no-answer' | 'timed-out' | 'server-fault' | 'sign-in-rejected' | 'app-fault'

type Failure = { kind: SpaTextKind } | { kind: 'refused'; text: string }

const textOf: Record<SpaTextKind, string> = {
  'no-answer': 'The app could not reach the server. Check your connection, then try again.',
  'timed-out': 'The server did not answer in time. Reload the page to see the latest data, then try again.',
  'server-fault': 'The server could not complete the request. Try again in a moment.',
  'sign-in-rejected':
    'The Server did not accept your sign-in for this request. Try again in a moment. If it fails again, log out and log in again.',
  'app-fault': 'The app has a fault. Reload the page, then try again.',
}

type UnexplainedKind = Exclude<SpaTextKind, 'app-fault'>

class UnexplainedFailureError extends Error {
  readonly kind: UnexplainedKind

  constructor(kind: UnexplainedKind, message: string) {
    super(message)
    this.name = 'UnexplainedFailureError'
    this.kind = kind
  }
}

const kindOfEmptyAnswer: Partial<Record<number, UnexplainedKind>> = {
  401: 'sign-in-rejected',
  504: 'timed-out',
}

export function answerError(status: number, body: unknown): ProblemDetailsError | UnexplainedFailureError {
  if (isProblemDetails(body)) return new ProblemDetailsError(body)
  return new UnexplainedFailureError(kindOfEmptyAnswer[status] ?? 'server-fault', `The answer has status ${status} and no problem details.`)
}

export function requestError(error: AxiosError): ProblemDetailsError | UnexplainedFailureError {
  if (error.response) return answerError(error.response.status, error.response.data)
  return new UnexplainedFailureError(error.code === AxiosError.ETIMEDOUT ? 'timed-out' : 'no-answer', error.message)
}

function refusalTextOf({ status, detail, title }: ProblemDetails): string | null {
  if ((status ?? 500) >= 500) return null
  return detail || title || null
}

function failureOf(error: unknown): Failure {
  if (error instanceof UnexplainedFailureError) return { kind: error.kind }
  if (!isProblemDetailsError(error)) return { kind: 'app-fault' }
  if (error.problemDetails.status === 401) return { kind: 'sign-in-rejected' }
  const text = refusalTextOf(error.problemDetails)
  return text === null ? { kind: 'server-fault' } : { kind: 'refused', text }
}

export function failureText(error: unknown): string {
  const failure = failureOf(error)
  return failure.kind === 'refused' ? failure.text : textOf[failure.kind]
}

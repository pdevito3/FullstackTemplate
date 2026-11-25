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
    'The server did not accept your sign-in for this request. Try again in a moment. If it fails again, log out and log in again.',
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

const kindOfEmptyAnswer = new Map<number, UnexplainedKind>([
  [401, 'sign-in-rejected'],
  [504, 'timed-out'],
])

export function answerError(status: number, problem: ProblemDetails | null): ProblemDetailsError | UnexplainedFailureError {
  if (problem) return new ProblemDetailsError(problem)

  return new UnexplainedFailureError(kindOfEmptyAnswer.get(status) ?? 'server-fault', `The answer has status ${status} and no problem details.`)
}

export function requestError(error: AxiosError): ProblemDetailsError | UnexplainedFailureError {
  if (error.response) {
    const { status, data } = error.response

    return answerError(status, isProblemDetails(data) ? data : null)
  }

  return new UnexplainedFailureError(error.code === AxiosError.ETIMEDOUT ? 'timed-out' : 'no-answer', error.message)
}

function refusalTextOf({ status, detail, title }: ProblemDetails): string | null {
  if ((status ?? 500) >= 500) return null

  return detail || title || null
}

function failureOf(cause: unknown): Failure {
  if (cause instanceof UnexplainedFailureError) return { kind: cause.kind }

  if (!isProblemDetailsError(cause)) return { kind: 'app-fault' }

  if (cause.problemDetails.status === 401) return { kind: 'sign-in-rejected' }
  const text = refusalTextOf(cause.problemDetails)

  return text === null ? { kind: 'server-fault' } : { kind: 'refused', text }
}

export function failureText(cause: unknown): string {
  const failure = failureOf(cause)

  return failure.kind === 'refused' ? failure.text : textOf[failure.kind]
}

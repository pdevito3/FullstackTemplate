/**
 * RFC 7807 Problem Details for HTTP APIs
 * https://datatracker.ietf.org/doc/html/rfc7807
 *
 * This is the standard error response format from .NET APIs with Problem Details enabled.
 */

import type { ProblemDetails as ContractProblemDetails } from '@/api/generated/models/types/problem-details'

/**
 * The problem details of the contract, with the member that ASP.NET adds to each body (`traceId`)
 * and the members of a subtype (for example, `errors` on a validation problem).
 */
export type ProblemDetails = ContractProblemDetails & {
  traceId?: string
  [key: string]: unknown
}

/** The text for an answer that has no text for the user: a 5xx, or an answer that is not a problem. */
export const serverFaultMessage = 'The server could not complete the request. Try again in a moment.'

/** The text for a request that got no answer. */
export const noAnswerMessage = 'The app could not reach the server. Check your connection, then try again.'

// A refusal (a 4xx) has a text for the user. The text of a 5xx is the default title of ASP.NET or a detail for a
// developer, so the user gets the text of the SPA.
function messageOf({ status, detail, title }: ProblemDetails): string {
  if ((status ?? 500) >= 500) return serverFaultMessage
  return detail || title || serverFaultMessage
}

/**
 * Custom error class that wraps Problem Details responses
 */
export class ProblemDetailsError extends Error {
  public readonly problemDetails: ProblemDetails

  constructor(problemDetails: ProblemDetails) {
    super(messageOf(problemDetails))
    this.name = 'ProblemDetailsError'
    this.problemDetails = problemDetails

    // Maintains proper stack trace for where our error was thrown
    if (Error.captureStackTrace) {
      Error.captureStackTrace(this, ProblemDetailsError)
    }
  }

  get status(): number | undefined {
    return this.problemDetails.status ?? undefined
  }

  get title(): string | undefined {
    return this.problemDetails.title ?? undefined
  }

  get detail(): string | undefined {
    return this.problemDetails.detail ?? undefined
  }

  get traceId(): string | undefined {
    return this.problemDetails.traceId
  }

  /**
   * Returns a user-friendly message suitable for display
   */
  get displayMessage(): string {
    return this.message
  }
}

/**
 * Type guard to check if an error is a ProblemDetailsError
 */
export function isProblemDetailsError(error: unknown): error is ProblemDetailsError {
  return error instanceof ProblemDetailsError
}

/**
 * Type guard to check if an object is a Problem Details response
 */
export function isProblemDetails(obj: unknown): obj is ProblemDetails {
  if (typeof obj !== 'object' || obj === null) {
    return false
  }
  const pd = obj as Record<string, unknown>
  return (
    typeof pd.type === 'string' &&
    typeof pd.title === 'string' &&
    typeof pd.status === 'number'
  )
}

export const STALE_VERSION_PROBLEM_TYPE = 'urn:fullstacktemplate:problem:stale-version'

export function isStaleVersionError(error: unknown): boolean {
  return isProblemDetailsError(error) && error.problemDetails.type === STALE_VERSION_PROBLEM_TYPE
}

export function validationErrorsOf(error: unknown): Record<string, string[]> {
  if (!isProblemDetailsError(error)) {
    return {}
  }
  const { errors } = error.problemDetails
  if (typeof errors !== 'object' || errors === null) {
    return {}
  }
  return Object.fromEntries(
    Object.entries(errors).filter(
      (entry): entry is [string, string[]] => Array.isArray(entry[1]) && entry[1].every((m) => typeof m === 'string'),
    ),
  )
}

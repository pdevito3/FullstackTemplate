/**
 * RFC 7807 Problem Details for HTTP APIs
 * https://datatracker.ietf.org/doc/html/rfc7807
 *
 * This is the standard error response format from .NET APIs with Problem Details enabled.
 */

import * as z from 'zod'

import type { ProblemDetails as ContractProblemDetails } from '@/api/generated/models/types/problem-details'

/**
 * The problem details of the contract, with the member that ASP.NET adds to each body (`traceId`)
 * and the field errors of a validation problem. `validationErrorsOf` parses the errors.
 */
export type ProblemDetails = ContractProblemDetails & {
  traceId?: string
  errors?: unknown
}

/**
 * Custom error class that wraps Problem Details responses
 */
export class ProblemDetailsError extends Error {
  public readonly problemDetails: ProblemDetails

  constructor(problemDetails: ProblemDetails) {
    super(`The answer has status ${problemDetails.status} and the problem type ${problemDetails.type}.`)
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

  get traceId(): string | undefined {
    return this.problemDetails.traceId
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
  return (
    typeof obj === 'object' &&
    obj !== null &&
    'type' in obj &&
    typeof obj.type === 'string' &&
    'title' in obj &&
    typeof obj.title === 'string' &&
    'status' in obj &&
    typeof obj.status === 'number'
  )
}

export const STALE_VERSION_PROBLEM_TYPE = 'urn:fullstacktemplate:problem:stale-version'

export function isStaleVersionError(cause: unknown): boolean {
  return isProblemDetailsError(cause) && cause.problemDetails.type === STALE_VERSION_PROBLEM_TYPE
}

const errorsSchema = z.record(z.string(), z.unknown())
const messagesSchema = z.array(z.string())

/** The messages of each field of a validation problem. A field whose messages are not a list of text is left out. */
export function validationErrorsOf(cause: unknown): Record<string, string[]> {
  if (!isProblemDetailsError(cause)) {
    return {}
  }

  const errors = errorsSchema.safeParse(cause.problemDetails.errors)

  if (!errors.success) {
    return {}
  }

  return Object.fromEntries(
    Object.entries(errors.data).flatMap(([field, value]): [string, string[]][] => {
      const messages = messagesSchema.safeParse(value)

      return messages.success ? [[field, messages.data]] : []
    }),
  )
}

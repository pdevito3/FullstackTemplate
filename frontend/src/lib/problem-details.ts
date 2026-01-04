/**
 * RFC 7807 Problem Details for HTTP APIs
 * https://datatracker.ietf.org/doc/html/rfc7807
 *
 * This is the standard error response format from .NET APIs with Problem Details enabled.
 */

export interface StackFrame {
  filePath: string
  fileName: string
  function: string
  line: number
  preContextLine: number
  preContextCode: string[]
  contextCode: string[]
  postContextCode: string[]
}

export interface ExceptionDetail {
  message: string
  type: string
  raw: string
  stackFrames: StackFrame[]
}

export interface ProblemDetails {
  type: string
  title: string
  status: number
  detail?: string
  instance?: string
  traceId?: string
  exceptionDetails?: ExceptionDetail[]
  // Additional properties that may be included
  [key: string]: unknown
}

/**
 * Custom error class that wraps Problem Details responses
 */
export class ProblemDetailsError extends Error {
  public readonly problemDetails: ProblemDetails

  constructor(problemDetails: ProblemDetails) {
    super(problemDetails.detail || problemDetails.title)
    this.name = 'ProblemDetailsError'
    this.problemDetails = problemDetails

    // Maintains proper stack trace for where our error was thrown
    if (Error.captureStackTrace) {
      Error.captureStackTrace(this, ProblemDetailsError)
    }
  }

  get status(): number {
    return this.problemDetails.status
  }

  get title(): string {
    return this.problemDetails.title
  }

  get detail(): string | undefined {
    return this.problemDetails.detail
  }

  get traceId(): string | undefined {
    return this.problemDetails.traceId
  }

  /**
   * Returns a user-friendly message suitable for display
   */
  get displayMessage(): string {
    return this.problemDetails.detail || this.problemDetails.title || 'An error occurred'
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

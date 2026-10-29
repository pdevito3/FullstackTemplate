import type { ProblemDetails } from '@/lib/problem-details'

// The body of an ASP.NET error response. The SPA shows it as a problem only if `type`, `title`, and `status` are set.
class ProblemDetailsBuilder {
  private readonly problem: ProblemDetails

  private constructor(problem: ProblemDetails) {
    this.problem = problem
  }

  static create() {
    return new ProblemDetailsBuilder({
      type: 'https://tools.ietf.org/html/rfc9110#section-15.6.1',
      title: 'An error occurred while processing your request.',
      status: 500,
      traceId: '00-00000000000000000000000000000001-0000000000000001-01',
    })
  }

  saying(detail: string) {
    return new ProblemDetailsBuilder({ ...this.problem, detail })
  }

  /** The 409 of a save that sent an old version, as the Server sends it. */
  staleVersion() {
    const detail = 'This record changed after you loaded it. Reload it, then make your change again.'
    return new ProblemDetailsBuilder({
      ...this.problem,
      type: 'urn:fullstacktemplate:problem:stale-version',
      title: 'The record changed after you loaded it.',
      status: 409,
      detail,
      errors: { version: [detail] },
    })
  }

  /** The 404 of a record that does not exist, as the Server sends it. */
  notFound(detail: string) {
    return new ProblemDetailsBuilder({
      ...this.problem,
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
      title: 'The specified resource was not found.',
      status: 404,
      detail,
    })
  }

  /** The 422 of a request that the Server refused, with the messages of each request field. */
  invalid(errors: Record<string, string[]>) {
    return new ProblemDetailsBuilder({
      ...this.problem,
      type: 'https://tools.ietf.org/html/rfc4918#section-11.2',
      title: 'One or more validation errors occurred.',
      status: 422,
      errors,
    })
  }

  build(): ProblemDetails {
    return this.problem
  }
}

export const aProblem = () => ProblemDetailsBuilder.create()

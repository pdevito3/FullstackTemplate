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

  build(): ProblemDetails {
    return this.problem
  }
}

export const aProblem = () => ProblemDetailsBuilder.create()

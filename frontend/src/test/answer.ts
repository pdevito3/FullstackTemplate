/** A response that the page gets for a request of the app, instead of a response of a server. */
export interface Answer {
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  /** The pathname, for example `/api/v1/users/u_1`. */
  path: string
  /** The query parameters that the request must have. A request can have more. */
  query?: Record<string, string>
  /** @default 200 */
  status?: number
  headers?: Record<string, string>
  /** Sent as JSON. */
  body?: unknown
}

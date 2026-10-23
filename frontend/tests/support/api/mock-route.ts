export type HttpMethod = 'get' | 'post' | 'put' | 'patch' | 'delete'

export interface MockRoute {
  method: HttpMethod
  /** The full path, for example `/api/v1/weather` or `/bff/user`. */
  path: string
  status: number
  body?: unknown
  headers?: Record<string, string>
  /** Holds the answer, so that a later request can overtake this one. */
  delayMs?: number
}

// The SPA sends these paths to the BFF. The Vite dev server serves the source files of the app under /src,
// so a source file such as /src/api/generated/... does not match.
const mockedPrefixes = ['/api/', '/bff/']

export const isMockedPath = (pathname: string) => mockedPrefixes.some((prefix) => pathname.startsWith(prefix))

export interface ServedRequest {
  query: URLSearchParams
  headers: Headers
  body: unknown
}

// A mock matches on method and path alone. Only a test that reads the request back can prove
// what the application put on the wire.
export function requestRecorder() {
  const served: { method: HttpMethod; url: URL; headers: Headers; body: unknown }[] = []

  const all = (method: HttpMethod, path: string): ServedRequest[] =>
    served
      .filter((r) => r.method === method && r.url.pathname === path)
      .map((r) => ({ query: r.url.searchParams, headers: r.headers, body: r.body }))

  return {
    add(method: HttpMethod, url: URL, headers: Headers, body: unknown) {
      served.push({ method, url, headers, body })
    },
    all,
    last: (method: HttpMethod, path: string) => all(method, path).at(-1),
  }
}

import { AxiosError, AxiosHeaders, CanceledError, type AxiosAdapter, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { addTenant } from '@/api/generated/tenants/clients/add-tenant'
import { getTenant } from '@/api/generated/tenants/clients/get-tenant'
import { getTenantList } from '@/api/generated/tenants/clients/get-tenant-list'
import { Notification } from '@/components/notification'
import { apiClient, onUnauthorized } from '@/lib/api-client'
import { parsePagination } from '@/lib/pagination'
import { ProblemDetailsError } from '@/lib/problem-details'

vi.mock('@/components/notification', () => ({
  Notification: { error: vi.fn() },
}))

const tenantId = '0b9d5d8e-2f8a-4a43-9d36-5c3f6f4f0a11'

const validationProblem = {
  type: 'https://tools.ietf.org/html/rfc4918#section-11.2',
  title: 'Validation error',
  status: 422,
  detail: 'Please provide a name.',
  errors: { name: ['Please provide a name.'] },
}

function respond(
  config: InternalAxiosRequestConfig,
  status: number,
  data: unknown,
  headers: Record<string, string> = {},
): Promise<AxiosResponse> {
  const response: AxiosResponse = {
    data,
    status,
    statusText: String(status),
    headers: new AxiosHeaders({ 'content-type': status >= 400 ? 'application/problem+json' : 'application/json', ...headers }),
    config,
  }
  const validateStatus = config.validateStatus ?? ((s: number) => s >= 200 && s < 300)
  if (validateStatus(status)) return Promise.resolve(response)
  return Promise.reject(new AxiosError(`Request failed with status code ${status}`, undefined, config, undefined, response))
}

// The stub adapter replaces the network, so each request still goes through the apiClient interceptors.
let adapter: ReturnType<typeof vi.fn<AxiosAdapter>>
const originalAdapter = apiClient.defaults.adapter
let checkSession: ReturnType<typeof vi.fn<() => Promise<boolean>>>

beforeEach(() => {
  adapter = vi.fn<AxiosAdapter>()
  apiClient.defaults.adapter = adapter
  checkSession = vi.fn<() => Promise<boolean>>()
  onUnauthorized(checkSession)
})

afterEach(() => {
  apiClient.defaults.adapter = originalAdapter
  vi.mocked(Notification.error).mockClear()
})

describe('apiClient with a generated client', () => {
  it('sends the X-CSRF header and the cookies', async () => {
    adapter.mockImplementation((config) => respond(config, 200, { id: tenantId, name: 'Acme' }))

    await getTenant({ path: { id: tenantId } })

    const config = adapter.mock.calls[0][0]
    expect(config.headers.get('X-CSRF')).toBe('1')
    expect(config.withCredentials).toBe(true)
  })

  it('shows the detail of a 422 in a toast and rejects with a ProblemDetailsError', async () => {
    adapter.mockImplementation((config) => respond(config, 422, validationProblem))

    const error = await addTenant({ body: { name: '' } }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ProblemDetailsError)
    expect((error as ProblemDetailsError).status).toBe(422)
    expect(Notification.error).toHaveBeenCalledExactlyOnceWith('Please provide a name.')
    expect(checkSession).not.toHaveBeenCalled()
  })

  it('does not show a toast when suppressErrorToast is true', async () => {
    adapter.mockImplementation((config) => respond(config, 422, validationProblem))

    const error = await addTenant({ body: { name: '' }, options: { suppressErrorToast: true } }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ProblemDetailsError)
    expect(Notification.error).not.toHaveBeenCalled()
  })

  it('does not show a toast for a cancelled request', async () => {
    // A real adapter rejects with a CanceledError when the signal aborts, for example when TanStack Query drops an old query
    adapter.mockImplementation((config) => Promise.reject(new CanceledError(undefined, undefined, config)))

    const error = await getTenant({ path: { id: tenantId } }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(AxiosError)
    expect((error as AxiosError).code).toBe(AxiosError.ERR_CANCELED)
    expect(Notification.error).not.toHaveBeenCalled()
  })

  it('returns an error status as a result, with no toast, when throwOnError is false', async () => {
    adapter.mockImplementation((config) => respond(config, 422, validationProblem))

    const result = await addTenant({ body: { name: '' }, throwOnError: false })

    expect(result.status).toBe(422)
    expect(result.error).toEqual(validationProblem)
    expect(Notification.error).not.toHaveBeenCalled()
  })
})

describe('a generated list client', () => {
  // A list endpoint has a name that ends in List, and kubb.config.ts makes its client return the full response
  it('returns the full response, so that parsePagination can read the X-Pagination header', async () => {
    const tenants = [{ id: tenantId, name: 'Acme' }]
    const pagination = {
      totalCount: 11,
      pageSize: 10,
      currentPageSize: 1,
      currentStartIndex: 11,
      currentEndIndex: 11,
      pageNumber: 2,
      totalPages: 2,
      hasPrevious: true,
      hasNext: false,
    }
    adapter.mockImplementation((config) =>
      respond(config, 200, tenants, { 'X-Pagination': JSON.stringify(pagination) }),
    )

    const result = await getTenantList({ query: { pageNumber: 2 } })

    expect(result.data).toEqual(tenants)
    expect(parsePagination(result)).toEqual(pagination)
  })
})

describe('apiClient with a fault of the Server', () => {
  const serverFault = 'The server could not complete the request. Try again in a moment.'

  it.each([
    ['the default title of ASP.NET', { type: 'https://tools.ietf.org/html/rfc9110#section-15.6.1', title: 'An error occurred while processing your request.', status: 500 }],
    ['a detail for a developer', { type: 'https://tools.ietf.org/html/rfc9110#section-15.6.1', title: 'Error', status: 500, detail: 'Npgsql timeout' }],
  ])('shows the text of the SPA, not %s, for a 5xx problem', async (_, problem) => {
    adapter.mockImplementation((config) => respond(config, problem.status, problem))

    const error = await getTenant({ path: { id: tenantId } }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ProblemDetailsError)
    expect((error as Error).message).toBe(serverFault)
    expect(Notification.error).toHaveBeenCalledExactlyOnceWith(serverFault)
  })

  it.each([
    ['a message field', 503, { message: 'Down for maintenance.' }],
    ['an error field', 502, { error: 'Bad gateway.' }],
    ['no body', 502, ''],
    ['a status that is not a fault of the Server', 404, ''],
  ])('shows the text of the SPA for an answer with %s and no problem', async (_, status, body) => {
    adapter.mockImplementation((config) => respond(config, status, body))

    const error = await getTenant({ path: { id: tenantId } }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(Error)
    expect(error).not.toBeInstanceOf(ProblemDetailsError)
    expect((error as Error).message).toBe(serverFault)
    expect(Notification.error).toHaveBeenCalledExactlyOnceWith(serverFault)
  })
})

describe('apiClient with a 401 or with no answer', () => {
  // The Server answers 401 with no body to a call that needs a user, and the BFF passes it on.
  it('checks the session for a 401, and shows no toast when the session ended', async () => {
    checkSession.mockResolvedValue(true)
    adapter.mockImplementation((config) => respond(config, 401, ''))

    const error = await getTenant({ path: { id: tenantId } }).catch((e: unknown) => e)

    expect(error).not.toBeInstanceOf(ProblemDetailsError)
    expect(checkSession).toHaveBeenCalledOnce()
    expect(Notification.error).not.toHaveBeenCalled()
  })

  it('checks the session for a 401, and shows a toast when the session lives', async () => {
    checkSession.mockResolvedValue(false)
    adapter.mockImplementation((config) => respond(config, 401, ''))

    const error = await getTenant({ path: { id: tenantId } }).catch((e: unknown) => e)

    const signInRejected =
      'The Server did not accept your sign-in for this request. Try again in a moment. If it fails again, log out and log in again.'
    expect((error as Error).message).toBe(signInRejected)
    expect(checkSession).toHaveBeenCalledOnce()
    expect(Notification.error).toHaveBeenCalledExactlyOnceWith(signInRejected)
  })

  it('does not check the session for a 401 of the session check itself', async () => {
    adapter.mockImplementation((config) => respond(config, 401, ''))

    await apiClient.get('/bff/user', { suppressErrorToast: true }).catch(() => undefined)

    expect(checkSession).not.toHaveBeenCalled()
  })

  it('says that the app could not reach the server for a network error', async () => {
    adapter.mockImplementation((config) =>
      Promise.reject(new AxiosError('Network Error', AxiosError.ERR_NETWORK, config)),
    )

    const error = await getTenant({ path: { id: tenantId } }).catch((e: unknown) => e)

    expect(error).not.toBeInstanceOf(ProblemDetailsError)
    const noAnswer = 'The app could not reach the server. Check your connection, then try again.'
    expect((error as Error).message).toBe(noAnswer)
    expect(Notification.error).toHaveBeenCalledExactlyOnceWith(noAnswer)
  })
})

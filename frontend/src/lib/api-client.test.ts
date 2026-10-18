import { AxiosError, AxiosHeaders, CanceledError, type AxiosAdapter, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { addTenant } from '@/api/generated/tenants/clients/add-tenant'
import { getTenant } from '@/api/generated/tenants/clients/get-tenant'
import { Notification } from '@/components/notification'
import { apiClient } from '@/lib/api-client'
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

function respond(config: InternalAxiosRequestConfig, status: number, data: unknown): Promise<AxiosResponse> {
  const response: AxiosResponse = {
    data,
    status,
    statusText: String(status),
    headers: new AxiosHeaders({ 'content-type': status >= 400 ? 'application/problem+json' : 'application/json' }),
    config,
  }
  const validateStatus = config.validateStatus ?? ((s: number) => s >= 200 && s < 300)
  if (validateStatus(status)) return Promise.resolve(response)
  return Promise.reject(new AxiosError(`Request failed with status code ${status}`, undefined, config, undefined, response))
}

// The stub adapter replaces the network, so each request still goes through the apiClient interceptors.
let adapter: ReturnType<typeof vi.fn<AxiosAdapter>>
const originalAdapter = apiClient.defaults.adapter

beforeEach(() => {
  adapter = vi.fn<AxiosAdapter>()
  apiClient.defaults.adapter = adapter
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

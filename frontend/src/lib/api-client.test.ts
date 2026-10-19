import { AxiosError, AxiosHeaders, CanceledError, type AxiosAdapter, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getWeatherForecast } from '@/api/generated/weather/clients/get-weather-forecast'
import { Notification } from '@/components/notification'
import { apiClient } from '@/lib/api-client'
import { ProblemDetailsError } from '@/lib/problem-details'

vi.mock('@/components/notification', () => ({
  Notification: { error: vi.fn() },
}))

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
    adapter.mockImplementation((config) => respond(config, 200, []))

    await getWeatherForecast()

    const config = adapter.mock.calls[0][0]
    expect(config.headers.get('X-CSRF')).toBe('1')
    expect(config.withCredentials).toBe(true)
  })

  it('shows the detail of a 422 in a toast and rejects with a ProblemDetailsError', async () => {
    adapter.mockImplementation((config) => respond(config, 422, validationProblem))

    const error = await getWeatherForecast().catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ProblemDetailsError)
    expect((error as ProblemDetailsError).status).toBe(422)
    expect(Notification.error).toHaveBeenCalledExactlyOnceWith('Please provide a name.')
  })

  it('does not show a toast when suppressErrorToast is true', async () => {
    adapter.mockImplementation((config) => respond(config, 422, validationProblem))

    const error = await getWeatherForecast({ options: { suppressErrorToast: true } }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ProblemDetailsError)
    expect(Notification.error).not.toHaveBeenCalled()
  })

  it('does not show a toast for a cancelled request', async () => {
    // A real adapter rejects with a CanceledError when the signal aborts, for example when TanStack Query drops an old query
    adapter.mockImplementation((config) => Promise.reject(new CanceledError(undefined, undefined, config)))

    const error = await getWeatherForecast().catch((e: unknown) => e)

    expect(error).toBeInstanceOf(AxiosError)
    expect((error as AxiosError).code).toBe(AxiosError.ERR_CANCELED)
    expect(Notification.error).not.toHaveBeenCalled()
  })

  it('returns an error status as a result, with no toast, when throwOnError is false', async () => {
    adapter.mockImplementation((config) => respond(config, 422, validationProblem))

    const result = await getWeatherForecast({ throwOnError: false })

    expect(result.status).toBe(422)
    expect(result.error).toEqual(validationProblem)
    expect(Notification.error).not.toHaveBeenCalled()
  })
})

describe('apiClient with a response that is not problem details', () => {
  it.each([
    ['the message field', { message: 'Down for maintenance.' }, 'Down for maintenance.'],
    ['the error field', { error: 'Bad gateway.' }, 'Bad gateway.'],
    ['the axios message when the body has neither field', { reason: 'x' }, 'Request failed with status code 503'],
  ])('rejects with %s', async (_, body, message) => {
    adapter.mockImplementation((config) => respond(config, 503, body))

    const error = await getWeatherForecast().catch((e: unknown) => e)

    expect(error).toBeInstanceOf(Error)
    expect(error).not.toBeInstanceOf(ProblemDetailsError)
    expect((error as Error).message).toBe(message)
    expect(Notification.error).toHaveBeenCalledExactlyOnceWith(message)
  })

  it('rejects with the axios message for a 401 with an empty body', async () => {
    // The Server sends a 401 with no body, and the BFF passes it on
    adapter.mockImplementation((config) => respond(config, 401, ''))

    const error = await getWeatherForecast().catch((e: unknown) => e)

    expect(error).not.toBeInstanceOf(ProblemDetailsError)
    expect((error as Error).message).toBe('Request failed with status code 401')
  })

  it('rejects with the axios message for a network error', async () => {
    adapter.mockImplementation((config) =>
      Promise.reject(new AxiosError('Network Error', AxiosError.ERR_NETWORK, config)),
    )

    const error = await getWeatherForecast().catch((e: unknown) => e)

    expect(error).not.toBeInstanceOf(ProblemDetailsError)
    expect((error as Error).message).toBe('Network Error')
    expect(Notification.error).toHaveBeenCalledExactlyOnceWith('Network Error')
  })
})

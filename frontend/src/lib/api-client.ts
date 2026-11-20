import axios, { type AxiosError, type AxiosRequestConfig } from 'axios'

import type { client as KubbClient } from '@/api/generated/.kubb/client'
import { Notification } from '@/components/notification'

import { failureText, requestError } from './request-failure'

declare module 'axios' {
  interface AxiosRequestConfig {
    /**
     * If true, suppresses automatic error toast notifications.
     * Use this when you want to handle errors manually in your component.
     * A generated client takes it through `options`, for example `getTenant({ path, options: { suppressErrorToast: true } })`.
     * @default false
     */
    suppressErrorToast?: boolean
  }
}

/**
 * Axios instance configured for BFF API calls with CSRF protection and credentials.
 *
 * The generated clients use this instance. Call a generated client for an endpoint of the Server:
 *   import { getTenant } from '@/api/generated/tenants/clients/get-tenant'
 *   const tenant = await getTenant({ path: { id } })
 *
 * Use apiClient directly only for an endpoint that is not in the OpenAPI document:
 *   const response = await apiClient.get<BffClaim[]>('/bff/user')
 *
 * Error Handling:
 *   By default, errors are displayed as toast notifications.
 *   To suppress this and handle errors manually:
 *
 *   await getTenant({ path: { id }, options: { suppressErrorToast: true } })
 */
export const apiClient = axios.create({
  withCredentials: true, // No effect on the same-origin calls. Only a cross-origin base URL needs it to send the cookie
  headers: {
    'X-CSRF': '1', // BFF CSRF protection header
  },
  // A request that reaches its time limit fails with ETIMEDOUT, not with the ECONNABORTED of an abort
  transitional: { clarifyTimeoutError: true },
})

// Over Bff:Proxy:ActivityTimeout (30 s) and Bff:Proxy:UploadActivityTimeout (5 min), so that the 504 of the BFF comes
// first. The upload limit also counts the time to send the file.
const requestTimeoutMs = 35_000
const uploadTimeoutMs = 10 * 60_000

apiClient.interceptors.request.use((config) => {
  config.timeout ||= config.data instanceof FormData ? uploadTimeoutMs : requestTimeoutMs
  return config
})

let isSessionEnded = () => Promise.resolve(false)

/**
 * Sets the check that tells whether the BFF session ended. The Server answers 401 to a call that needs a user, and the
 * BFF passes it on. That 401 can also come while the session lives, for example when a token refresh fails for a
 * short time. `main.tsx` checks the session again, so that the root route goes to the login when the session ended.
 */
export function onUnauthorized(handler: () => Promise<boolean>) {
  isSessionEnded = handler
}

// Response interceptor to handle errors with Problem Details support
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    // Get custom config options
    const config = error.config as AxiosRequestConfig | undefined
    const suppressToast = config?.suppressErrorToast ?? false

    // A cancel (for example, TanStack Query aborts an old query) is not an error to show.
    if (!axios.isAxiosError(error) || axios.isCancel(error)) {
      return Promise.reject(error)
    }

    const processedError = requestError(error)

    // The check of the session cannot wait for itself, so its own 401 is a failed check.
    if (error.response?.status === 401 && config?.url !== '/bff/user') {
      if (!(await isSessionEnded()) && !suppressToast) Notification.error(failureText(processedError))
      return Promise.reject(processedError)
    }

    // Show toast notification unless suppressed
    if (!suppressToast) {
      Notification.error(failureText(processedError))
    }

    return Promise.reject(processedError)
  }
)

// The generated clients send each request through apiClient, so they get the CSRF header, the cookies, and the error toasts.
// Each API version has its own generated folder (src/api/generated, src/api/generated-v2), and each folder has its own client.
const kubbClients = import.meta.glob<{ client: typeof KubbClient }>('/src/api/generated*/.kubb/client.ts', { eager: true })
for (const { client } of Object.values(kubbClients)) {
  client.setConfig({ transport: apiClient })
}

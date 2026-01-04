import axios, { type AxiosRequestConfig, type AxiosError } from 'axios'
import {
  ProblemDetailsError,
  isProblemDetails,
  type ProblemDetails,
} from './problem-details'
import { Notification } from '@/components/notifications'

/**
 * Extended Axios config with Problem Details handling options
 */
export interface ApiClientRequestConfig extends AxiosRequestConfig {
  /**
   * If true, suppresses automatic error toast notifications.
   * Use this when you want to handle errors manually in your component.
   * @default false
   */
  suppressErrorToast?: boolean
}

/**
 * Axios instance configured for BFF API calls with CSRF protection and credentials.
 *
 * Usage:
 *   import { apiClient } from '@/lib/api-client'
 *   const response = await apiClient.get('/api/weather')
 *   const data = await apiClient.post('/api/something', { body: data })
 *
 * Error Handling:
 *   By default, errors are displayed as toast notifications.
 *   To suppress this and handle errors manually:
 *
 *   const response = await apiClient.get('/api/something', { suppressErrorToast: true })
 */
export const apiClient = axios.create({
  withCredentials: true, // Required to send cookies cross-origin
  headers: {
    'X-CSRF': '1', // BFF CSRF protection header
  },
})

/**
 * Extracts a user-friendly error message from a Problem Details response
 */
function getErrorMessage(problemDetails: ProblemDetails): string {
  // Prefer detail as it's usually the most specific message
  if (problemDetails.detail) {
    return problemDetails.detail
  }
  // Fall back to title
  return problemDetails.title || 'An error occurred'
}

/**
 * Handles error responses, converting to ProblemDetailsError when applicable
 */
function handleErrorResponse(error: AxiosError): ProblemDetailsError | Error {
  const responseData = error.response?.data

  // Check if response follows Problem Details format
  if (isProblemDetails(responseData)) {
    return new ProblemDetailsError(responseData)
  }

  // Handle non-Problem Details responses
  // Try to extract a message from common error response formats
  if (responseData && typeof responseData === 'object') {
    const data = responseData as Record<string, unknown>
    const message =
      typeof data.message === 'string'
        ? data.message
        : typeof data.error === 'string'
          ? data.error
          : error.message

    return new Error(message)
  }

  return new Error(error.message || 'An unexpected error occurred')
}

// Response interceptor to handle errors with Problem Details support
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    // Get custom config options
    const config = error.config as ApiClientRequestConfig | undefined
    const suppressToast = config?.suppressErrorToast ?? false

    if (!axios.isAxiosError(error)) {
      // Not an Axios error, re-throw as-is
      return Promise.reject(error)
    }

    // Convert to appropriate error type
    const processedError = handleErrorResponse(error)

    // Show toast notification unless suppressed
    if (!suppressToast) {
      const message =
        processedError instanceof ProblemDetailsError
          ? getErrorMessage(processedError.problemDetails)
          : processedError.message

      Notification.error(message)
    }

    return Promise.reject(processedError)
  }
)

/**
 * Generic fetch function for custom API calls
 */
export async function apiFetch<T>(
  url: string,
  config?: ApiClientRequestConfig
): Promise<T> {
  const response = await apiClient.request<T>({ url, ...config })
  return response.data
}

/**
 * Helper to make API calls that suppress error toasts.
 * Use when you want to handle errors in your component's UI.
 *
 * @example
 * const { data, error } = useQuery({
 *   queryKey: ['users', id],
 *   queryFn: () => fetchWithoutToast('/api/users/' + id),
 * })
 *
 * if (error) {
 *   // Handle error in component
 * }
 */
export async function fetchWithoutToast<T>(
  url: string,
  config?: Omit<ApiClientRequestConfig, 'suppressErrorToast'>
): Promise<T> {
  return apiFetch<T>(url, { ...config, suppressErrorToast: true })
}

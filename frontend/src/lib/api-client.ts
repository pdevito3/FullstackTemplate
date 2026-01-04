import axios, { type AxiosRequestConfig } from 'axios'

/**
 * Axios instance configured for BFF API calls with CSRF protection and credentials.
 *
 * Usage:
 *   import { apiClient } from '@/domain/shared/api-client'
 *   const response = await apiClient.get('/api/weather')
 *   const data = await apiClient.post('/api/something', { body: data })
 */
export const apiClient = axios.create({
  withCredentials: true, // Required to send cookies cross-origin
  headers: {
    'X-CSRF': '1', // BFF CSRF protection header
  },
})

// Response interceptor to handle errors
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // Re-throw axios errors with more context
    if (axios.isAxiosError(error)) {
      const message = error.response?.data?.message || error.message
      return Promise.reject(new Error(message))
    }
    return Promise.reject(error)
  }
)

/**
 * Generic fetch function for custom API calls
 */
export async function apiFetch<T>(
  url: string,
  config?: AxiosRequestConfig
): Promise<T> {
  const response = await apiClient.request<T>({ url, ...config })
  return response.data
}

import axios, { type AxiosRequestConfig } from "axios";

/**
 * Axios instance configured for BFF API calls with CSRF protection and credentials.
 *
 * Usage:
 *   import { apiClient } from './api/client'
 *   const response = await apiClient.get('/api/weather')
 *   const data = await apiClient.post('/api/something', { body: data })
 */
export const apiClient = axios.create({
  withCredentials: true, // Required to send cookies cross-origin
  headers: {
    "X-CSRF": "1", // BFF CSRF protection header
  },
});

// Response interceptor to extract data automatically
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // Re-throw axios errors with more context
    if (axios.isAxiosError(error)) {
      const message = error.response?.data?.message || error.message;
      return Promise.reject(new Error(message));
    }
    return Promise.reject(error);
  }
);

/**
 * Type definitions for API responses
 */
export interface WeatherForecast {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
}

export interface SecureWeatherForecast extends WeatherForecast {
  requestedBy: string;
}

export interface BffClaim {
  type: string;
  value: string;
}

/**
 * Weather API functions
 */
export const weatherApi = {
  // Public endpoint - no authentication required
  getForecasts: async (): Promise<WeatherForecast[]> => {
    const response = await apiClient.get<WeatherForecast[]>(`/api/v1/weather`);
    return response.data;
  },
  // Secure endpoint - requires authentication
  getSecureForecasts: async (): Promise<SecureWeatherForecast[]> => {
    const response = await apiClient.get<SecureWeatherForecast[]>(
      `/api/v1/weather/secure`
    );
    return response.data;
  },
};

/**
 * Auth API functions
 */
export const authApi = {
  getUser: async (): Promise<BffClaim[]> => {
    const response = await apiClient.get<BffClaim[]>("/bff/user");
    return response.data;
  },

  login: (returnUrl: string = "/"): void => {
    window.location.href = `/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  },

  logout: (logoutUrl?: string): void => {
    window.location.href = logoutUrl || "/bff/logout";
  },
};

/**
 * Generic fetch function for custom API calls
 */
export async function apiFetch<T>(
  url: string,
  config?: AxiosRequestConfig
): Promise<T> {
  const response = await apiClient.request<T>({ url, ...config });
  return response.data;
}

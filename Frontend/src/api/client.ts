import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { rawClient } from './rawClient'
import type { AuthResponse } from './types'
import { announceSessionExpired, getAccessToken, setAccessToken } from '../auth/tokenStore'

/** The authenticated instance — every product/warehouse call goes through this. */
export const client = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '',
  withCredentials: true,
})

client.interceptors.request.use((config) => {
  const token = getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Single-flight guard: if several requests 401 at once (e.g. a page that fires off
// four API calls right as the access token expires), they all share one refresh call
// instead of racing four separate refreshes against the rotating refresh token.
let refreshPromise: Promise<AuthResponse> | null = null

async function refreshAccessToken(): Promise<AuthResponse> {
  if (!refreshPromise) {
    refreshPromise = rawClient
      .post<AuthResponse>('/api/auth/refresh')
      .then((res) => res.data)
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _retried?: boolean
}

client.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as RetryableRequestConfig | undefined

    if (error.response?.status !== 401 || !original || original._retried) {
      return Promise.reject(error)
    }

    original._retried = true

    try {
      const { accessToken, expiresAtUtc } = await refreshAccessToken()
      setAccessToken(accessToken, expiresAtUtc)
      original.headers.Authorization = `Bearer ${accessToken}`
      return client(original)
    } catch (refreshError) {
      announceSessionExpired()
      return Promise.reject(refreshError)
    }
  },
)

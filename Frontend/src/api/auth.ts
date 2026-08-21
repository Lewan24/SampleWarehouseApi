import { rawClient } from './rawClient'
import type { AuthResponse, RegisteredUser } from './types'

export interface RegisterPayload {
  email: string
  password: string
  confirmPassword: string
}

export interface LoginPayload {
  email: string
  password: string
}

export async function register(payload: RegisterPayload): Promise<RegisteredUser> {
  const { data } = await rawClient.post<RegisteredUser>('/api/auth/register', payload)
  return data
}

export async function login(payload: LoginPayload): Promise<AuthResponse> {
  const { data } = await rawClient.post<AuthResponse>('/api/auth/login', payload)
  return data
}

/** Uses the httpOnly refresh cookie automatically attached by the browser — no body needed. */
export async function refresh(): Promise<AuthResponse> {
  const { data } = await rawClient.post<AuthResponse>('/api/auth/refresh')
  return data
}

export async function revoke(accessToken: string | null): Promise<void> {
  await rawClient.post(
    '/api/auth/revoke',
    {},
    accessToken ? { headers: { Authorization: `Bearer ${accessToken}` } } : undefined,
  )
}

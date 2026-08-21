import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import * as authApi from '../api/auth'
import type { LoginPayload, RegisterPayload } from '../api/auth'
import { decodeAccessToken } from '../lib/jwt'
import type { AccessTokenInfo } from '../lib/jwt'
import {
  SESSION_EXPIRED_EVENT,
  getAccessToken,
  setAccessToken as storeAccessToken,
  subscribe,
} from './tokenStore'
import type { Role } from '../api/types'

interface AuthContextValue {
  user: AccessTokenInfo | null
  isAuthenticated: boolean
  /** True while the initial silent-refresh-on-load check is in flight. */
  isLoading: boolean
  login: (payload: LoginPayload) => Promise<void>
  register: (payload: RegisterPayload) => Promise<void>
  logout: () => Promise<void>
  /** UI-only convenience — never the source of truth for what a request is allowed to do. */
  hasRole: (...roles: Role[]) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

// Refresh this many milliseconds before the access token actually expires, so a call
// firing right at the boundary doesn't get caught by a token that just lapsed.
const REFRESH_MARGIN_MS = 60_000

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(getAccessToken())
  const [isLoading, setIsLoading] = useState(true)
  const refreshTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const clearRefreshTimer = () => {
    if (refreshTimer.current) {
      clearTimeout(refreshTimer.current)
      refreshTimer.current = null
    }
  }

  const scheduleRefresh = useCallback((expiresAtUtc: string) => {
    clearRefreshTimer()
    const delay = Math.max(new Date(expiresAtUtc).getTime() - Date.now() - REFRESH_MARGIN_MS, 0)
    refreshTimer.current = setTimeout(async () => {
      try {
        const result = await authApi.refresh()
        storeAccessToken(result.accessToken, result.expiresAtUtc)
        scheduleRefresh(result.expiresAtUtc)
      } catch {
        storeAccessToken(null, null)
      }
    }, delay)
  }, [])

  // Keep local state in sync whenever anything (login, the axios 401 interceptor,
  // the proactive timer above) updates the shared token store.
  useEffect(() => {
    return subscribe(() => setToken(getAccessToken()))
  }, [])

  useEffect(() => {
    const onSessionExpired = () => setToken(null)
    window.addEventListener(SESSION_EXPIRED_EVENT, onSessionExpired)
    return () => window.removeEventListener(SESSION_EXPIRED_EVENT, onSessionExpired)
  }, [])

  // On first load, there's no access token in memory yet (a full page reload clears
  // it) — try to silently re-establish the session from the httpOnly refresh cookie
  // before rendering anything that depends on auth state.
  useEffect(() => {
    let cancelled = false

    authApi
      .refresh()
      .then((result) => {
        if (cancelled) return
        storeAccessToken(result.accessToken, result.expiresAtUtc)
        scheduleRefresh(result.expiresAtUtc)
      })
      .catch(() => {
        // No valid session cookie — that's a normal "not logged in" state, not an error.
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false)
      })

    return () => {
      cancelled = true
      clearRefreshTimer()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const login = useCallback(
    async (payload: LoginPayload) => {
      const result = await authApi.login(payload)
      storeAccessToken(result.accessToken, result.expiresAtUtc)
      scheduleRefresh(result.expiresAtUtc)
    },
    [scheduleRefresh],
  )

  const register = useCallback(async (payload: RegisterPayload) => {
    await authApi.register(payload)
  }, [])

  const logout = useCallback(async () => {
    clearRefreshTimer()
    try {
      await authApi.revoke(getAccessToken())
    } finally {
      storeAccessToken(null, null)
    }
  }, [])

  const user = useMemo(() => (token ? decodeAccessToken(token) : null), [token])

  const hasRole = useCallback(
    (...roles: Role[]) => !!user && roles.some((role) => user.roles.includes(role)),
    [user],
  )

  const value: AuthContextValue = {
    user,
    isAuthenticated: !!user,
    isLoading,
    login,
    register,
    logout,
    hasRole,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}

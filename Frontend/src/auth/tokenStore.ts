/**
 * Holds the current access token in memory only (a plain module-level variable —
 * never localStorage/sessionStorage, which JavaScript-reachable storage an XSS bug
 * could read). It's deliberately outside React so both the axios interceptor and
 * AuthContext can reach it without importing each other and creating a cycle.
 *
 * Losing this on a full page reload is intentional and fine: AuthProvider calls
 * /api/auth/refresh on startup to re-establish it from the httpOnly refresh cookie.
 */
let accessToken: string | null = null
let accessTokenExpiresAtUtc: string | null = null

type Listener = () => void
const listeners = new Set<Listener>()

export function getAccessToken(): string | null {
  return accessToken
}

export function getAccessTokenExpiry(): string | null {
  return accessTokenExpiresAtUtc
}

export function setAccessToken(token: string | null, expiresAtUtc: string | null): void {
  accessToken = token
  accessTokenExpiresAtUtc = expiresAtUtc
  listeners.forEach((listener) => listener())
}

export function subscribe(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** Fired when a silent refresh fails outside of a user-initiated action (e.g. an
 * expired session discovered mid-request). AuthContext listens for this to update
 * its state and send the user back to the login page. */
export const SESSION_EXPIRED_EVENT = 'warehouse:session-expired'

export function announceSessionExpired(): void {
  setAccessToken(null, null)
  window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT))
}

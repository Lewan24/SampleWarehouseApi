import type { Role } from '../api/types'

interface AccessTokenClaims {
  sub?: string
  email?: string
  /** ASP.NET Core Identity emits the long ClaimTypes.Role URI as the JSON key. */
  role?: Role | Role[]
  [key: string]: unknown
}

export interface AccessTokenInfo {
  userId: string
  email: string
  roles: Role[]
}

/**
 * Reads the payload of a JWT for UI purposes only — e.g. deciding which nav links or
 * buttons to show. This never verifies the token's signature (the browser doesn't have
 * the signing key, and shouldn't). It is NOT a security check: every request is still
 * authorized independently by the API on every call. Treat anything derived from this
 * as "what the UI should probably show", never as "what the user is allowed to do".
 */
export function decodeAccessToken(token: string): AccessTokenInfo | null {
  try {
    const [, payloadSegment] = token.split('.')
    if (!payloadSegment) return null

    const base64 = payloadSegment.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
    const json = atob(padded)
    const claims = JSON.parse(json) as AccessTokenClaims

    const roleClaim = claims.role ?? claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
    const roles = Array.isArray(roleClaim) ? roleClaim : roleClaim ? [roleClaim as Role] : []

    return {
      userId: claims.sub ?? '',
      email: claims.email ?? '',
      roles,
    }
  } catch {
    return null
  }
}

/** True once `expiresAtUtc` is within `bufferMs` of now — used to schedule proactive refresh. */
export function isExpiringSoon(expiresAtUtc: string, bufferMs = 60_000): boolean {
  return new Date(expiresAtUtc).getTime() - Date.now() < bufferMs
}

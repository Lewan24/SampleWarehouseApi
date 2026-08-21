import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import type { Role } from '../api/types'
import { FullPageSpinner } from '../components/Spinner'

interface RequireAuthProps {
  children: ReactNode
  /** If given, at least one of these roles is required — otherwise any authenticated user passes. */
  roles?: Role[]
}

/**
 * Route guard. This is a UX convenience — it keeps people from landing on a page
 * they can't use and getting a confusing error. It is NOT the security boundary:
 * every API call the resulting page makes is independently authorized by the server,
 * which is what actually enforces access control.
 */
export function RequireAuth({ children, roles }: RequireAuthProps) {
  const { isAuthenticated, isLoading, hasRole } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return <FullPageSpinner />
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (roles && !hasRole(...roles)) {
    return <Navigate to="/forbidden" replace />
  }

  return <>{children}</>
}

import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/shared/contexts/AuthContext'
import type { UserRole } from '@/types/auth.types'
import { CUSTOMER_ROLES } from '@/types/auth.types'
import { ROUTES } from '@/shared/constants'

interface ProtectedRouteProps {
  allowedRoles?: UserRole[]
}

export function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, user } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to={ROUTES.LOGIN} state={{ from: location }} replace />
  }

  // Role-based guard: if specific roles required, redirect if user doesn't match
  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    // CUSTOMER redirects to profile, staff/admin redirects to dashboard
    const fallback = CUSTOMER_ROLES.includes(user.role) ? ROUTES.PROFILE : ROUTES.DASHBOARD
    return <Navigate to={fallback} replace />
  }

  return <Outlet />
}

/**
 * Guard for PWA Profile & KYC pages:
 * Allows all authenticated users (CUSTOMER, ADMIN, MANAGER, STAFF)
 * to access their profile and complete KYC.
 */
export function CustomerRoute() {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to={ROUTES.LOGIN} state={{ from: location }} replace />
  }

  return <Outlet />
}

/**
 * Guard allowing only ADMIN/MANAGER/STAFF (used for DashboardLayout routes).
 * CUSTOMER is redirected to vehicle search.
 */
export function AdminRoute() {
  const { isAuthenticated, isLoading, user } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    )
  }

  // Unauthenticated guests:
  // If navigating to root '/' -> redirect to '/search' for public vehicle browsing
  // If navigating to admin subpages (such as '/users') -> redirect to '/login'
  if (!isAuthenticated) {
    if (location.pathname === '/' || location.pathname === ROUTES.DASHBOARD) {
      return <Navigate to={ROUTES.SEARCH} replace />
    }
    return <Navigate to={ROUTES.LOGIN} state={{ from: location }} replace />
  }

  // CUSTOMER entering admin routes -> redirect to vehicle search page
  if (user && CUSTOMER_ROLES.includes(user.role)) {
    return <Navigate to={ROUTES.SEARCH} replace />
  }

  return <Outlet />
}

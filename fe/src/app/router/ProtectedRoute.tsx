import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/shared/contexts/AuthContext'
import type { UserRole } from '@/types/auth.types'
import { CUSTOMER_ROLES, OWNER_ROLES } from '@/types/auth.types'
import { ROUTES } from '@/shared/constants'

interface ProtectedRouteProps {
  allowedRoles?: UserRole[]
}

export function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, user } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div role="status" aria-live="polite" className="flex h-screen items-center justify-center">
        <div aria-hidden="true" className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        <span className="sr-only">Checking your account…</span>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to={ROUTES.PROFILE_AUTH} state={{ from: location }} replace />
  }

  // Role-based guard: if specific roles required, redirect if user doesn't match
  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    const fallback = user.role === 'OWNER'
      ? ROUTES.OWNER_DASHBOARD
      : CUSTOMER_ROLES.includes(user.role) ? ROUTES.PROFILE : ROUTES.DASHBOARD
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
    return <Navigate to={ROUTES.PROFILE_AUTH} state={{ from: location }} replace />
  }

  return <Outlet />
}

/**
 * Guard for the administrator workspace. API permissions remain enforced by the backend.
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

  // Unauthenticated visitors can browse public routes; admin pages require sign in.
  if (!isAuthenticated) {
    return <Navigate to={ROUTES.ADMIN_LOGIN} state={{ from: location }} replace />
  }

  // The admin workspace actions are protected by the backend's AdminOnly policy.
  if (user && user.role !== 'ADMIN') {
    return <Navigate to={ROUTES.ADMIN_LOGIN} state={{ from: location }} replace />
  }

  return <Outlet />
}

/** Entry route for admins who already have an active session. */
export function AdminEntryRoute() {
  const { isAuthenticated, isLoading, user } = useAuth()

  if (isLoading) {
    return (
      <div className="flex min-h-dvh items-center justify-center">
        <div className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    )
  }

  if (isAuthenticated && user?.role === 'ADMIN') {
    return <Navigate to={ROUTES.DASHBOARD} replace />
  }

  if (isAuthenticated) return <Navigate to={ROUTES.ADMIN_LOGIN} state={{ from: { pathname: ROUTES.DASHBOARD } }} replace />
  return <Navigate to={ROUTES.ADMIN_LOGIN} replace />
}

/** Keeps the admin sign-in page outside the mobile customer PWA shell. */
export function AdminLoginRoute() {
  const { isAuthenticated, isLoading, user } = useAuth()

  if (isLoading) {
    return (
      <div className="flex min-h-dvh items-center justify-center">
        <div className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    )
  }

  if (isAuthenticated && user?.role === 'ADMIN') return <Navigate to={ROUTES.DASHBOARD} replace />
  return <Outlet />
}

/** Owner workspace pages are isolated from customer and admin routes. */
export function OwnerRoute() {
  return <ProtectedRoute allowedRoles={OWNER_ROLES} />
}

export function OwnerEntryRoute() {
  const { isAuthenticated, isLoading, user } = useAuth()
  if (isLoading) return <div className="flex min-h-dvh items-center justify-center"><div className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" /></div>
  if (isAuthenticated && user?.role === 'OWNER') return <Navigate to={ROUTES.OWNER_DASHBOARD} replace />
  return <Navigate to={ROUTES.OWNER_LOGIN} replace />
}

export function OwnerLoginRoute() {
  const { isAuthenticated, isLoading, user } = useAuth()
  if (isLoading) return <div className="flex min-h-dvh items-center justify-center"><div className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" /></div>
  if (isAuthenticated && user?.role === 'OWNER') return <Navigate to={ROUTES.OWNER_DASHBOARD} replace />
  return <Outlet />
}

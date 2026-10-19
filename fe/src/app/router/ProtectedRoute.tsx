import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/shared/contexts/AuthContext'
import type { UserRole } from '@/types/auth.types'
import { ADMIN_ROLES, CUSTOMER_ROLES } from '@/types/auth.types'
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
    // CUSTOMER đi về profile, staff/admin đi về dashboard
    const fallback = CUSTOMER_ROLES.includes(user.role) ? ROUTES.PROFILE : ROUTES.DASHBOARD
    return <Navigate to={fallback} replace />
  }

  return <Outlet />
}

/**
 * Guard cho trang PWA Profile & KYC:
 * Cho phép tất cả người dùng đã xác thực (CUSTOMER, ADMIN, MANAGER, STAFF)
 * truy cập để xem hồ sơ và thực hiện KYC.
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
 * Guard chỉ cho ADMIN/MANAGER/STAFF vào (dùng cho DashboardLayout routes).
 * CUSTOMER bị redirect về profile.
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

  // Khách chưa đăng nhập:
  // Nếu vào trang chủ '/' → dẫn sang '/search' để xem danh sách xe công khai
  // Nếu vào trang admin khác (như '/users') → dẫn sang '/login'
  if (!isAuthenticated) {
    if (location.pathname === '/' || location.pathname === ROUTES.DASHBOARD) {
      return <Navigate to={ROUTES.SEARCH} replace />
    }
    return <Navigate to={ROUTES.LOGIN} state={{ from: location }} replace />
  }

  // CUSTOMER vào trang admin → redirect về trang tìm thuê xe
  if (user && CUSTOMER_ROLES.includes(user.role)) {
    return <Navigate to={ROUTES.SEARCH} replace />
  }

  return <Outlet />
}

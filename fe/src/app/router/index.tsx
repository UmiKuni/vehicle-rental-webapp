import { lazy, Suspense } from 'react'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { ROUTES } from '@/shared/constants'
import { ProtectedRoute, AdminEntryRoute, AdminLoginRoute, AdminRoute } from './ProtectedRoute'
import { DashboardLayout } from '@/app/layouts/DashboardLayout'
import { MobileLayout } from '@/app/layouts/MobileLayout'

// Lazy loaded pages
const LoginPage        = lazy(() => import('@/features/auth/pages/LoginPage'))
const RegisterPage     = lazy(() => import('@/features/auth/pages/RegisterPage'))
const AuthPage = lazy(() => import('@/features/auth/pages/AuthPage'))
const DashboardPage    = lazy(() => import('@/features/dashboard/pages/DashboardPage'))
const NotFoundPage     = lazy(() => import('@/features/not-found/pages/NotFoundPage'))
const ProfilePage      = lazy(() => import('@/features/profile/pages/ProfilePage'))
const KycWizardPage    = lazy(() => import('@/features/profile/pages/KycWizardPage'))
const SearchVehiclesPage = lazy(() => import('@/features/vehicles/pages/SearchVehiclesPage'))
const HomePage = lazy(() => import('@/features/home/pages/HomePage'))
const VehicleDetailPage = lazy(() => import('@/features/vehicles/pages/VehicleDetailPage'))
const MyBookingsPage   = lazy(() => import('@/features/bookings/pages/MyBookingsPage'))
const BookingDetailPage = lazy(() => import('@/features/bookings/pages/BookingDetailPage'))
const AdminVehiclesPage = lazy(() => import('@/features/admin/pages/AdminVehiclesPage'))
const AdminCatalogPage = lazy(() => import('@/features/admin/pages/AdminCatalogPage'))
const AdminUsersPage = lazy(() => import('@/features/admin/pages/AdminUsersPage'))

function PageLoader() {
  return (
    <div className="flex h-[60vh] items-center justify-center">
      <div className="size-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
    </div>
  )
}

function wrap(Page: React.ComponentType) {
  return (
    <Suspense fallback={<PageLoader />}>
      <Page />
    </Suspense>
  )
}

const router = createBrowserRouter([
  {
    path: ROUTES.ADMIN_LOGIN,
    element: <AdminLoginRoute />,
    children: [{ index: true, element: wrap(LoginPage) }],
  },
  { path: ROUTES.LOGIN, element: wrap(LoginPage) },
  { path: ROUTES.REGISTER, element: wrap(RegisterPage) },
  { path: ROUTES.ADMIN, element: <AdminEntryRoute /> },

  // Keep existing bookmarks working as the admin URLs move under /admin.
  { path: '/dashboard', element: <Navigate to={ROUTES.DASHBOARD} replace /> },
  { path: '/dashboard/vehicles', element: <Navigate to={ROUTES.ADMIN_VEHICLES} replace /> },
  { path: '/dashboard/catalog', element: <Navigate to={ROUTES.ADMIN_CATALOG} replace /> },
  { path: '/users', element: <Navigate to={ROUTES.USERS} replace /> },

  // ── Public Auth Pages ───────────────────────────────────────────────────────
  // ── Root & Admin Desktop Dashboard ──────────────────────────────────────────
  {
    element: <AdminRoute />,
    children: [
      {
        element: <DashboardLayout />,
        children: [
          { path: ROUTES.DASHBOARD, element: wrap(DashboardPage) },
          { path: ROUTES.ADMIN_VEHICLES, element: wrap(AdminVehiclesPage) },
          { path: ROUTES.ADMIN_CATALOG, element: wrap(AdminCatalogPage) },
          { path: ROUTES.USERS, element: wrap(AdminUsersPage) },
        ],
      },
    ],
  },

  // ── Mobile PWA layout (Public vehicle browsing + Protected user pages) ────
  {
    element: <MobileLayout />,
    children: [
      { path: ROUTES.HOME, element: wrap(HomePage) },
      { path: ROUTES.PROFILE_AUTH, element: wrap(AuthPage) },
      // 1. PUBLIC: Guests & customers can browse and search vehicles
      {
        path: ROUTES.SEARCH,
        element: wrap(SearchVehiclesPage),
      },
      {
        path: '/vehicles/:vehicleId',
        element: wrap(VehicleDetailPage),
      },

      // 2. PROTECTED: Login required to view trips and profile
      {
        element: <ProtectedRoute />,
        children: [
          { path: ROUTES.BOOKINGS, element: wrap(MyBookingsPage) },
          { path: '/booking/:bookingId', element: wrap(BookingDetailPage) },
          { path: ROUTES.PROFILE,  element: wrap(ProfilePage) },
        ],
      },
    ],
  },

  // ── KYC wizard: full-screen, protected ──────────────────────────────────────
  {
    element: <ProtectedRoute />,
    children: [
      {
        path: ROUTES.PROFILE_VERIFY,
        element: wrap(KycWizardPage),
      },
    ],
  },

  // ── 404 ────────────────────────────────────────────────────────────────────
  {
    path: '*',
    element: wrap(NotFoundPage),
  },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}

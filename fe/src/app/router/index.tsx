import { lazy, Suspense } from 'react'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { ROUTES } from '@/shared/constants'
import { ProtectedRoute, AdminRoute } from './ProtectedRoute'
import { DashboardLayout } from '@/app/layouts/DashboardLayout'
import { MobileLayout } from '@/app/layouts/MobileLayout'

// Lazy loaded pages
const LoginPage        = lazy(() => import('@/features/auth/pages/LoginPage'))
const RegisterPage     = lazy(() => import('@/features/auth/pages/RegisterPage'))
const AuthPage = lazy(() => import('@/features/auth/pages/AuthPage'))
const DashboardPage    = lazy(() => import('@/features/dashboard/pages/DashboardPage'))
const UsersPage        = lazy(() => import('@/features/users/pages/UsersPage'))
const NotFoundPage     = lazy(() => import('@/features/not-found/pages/NotFoundPage'))
const ProfilePage      = lazy(() => import('@/features/profile/pages/ProfilePage'))
const KycWizardPage    = lazy(() => import('@/features/profile/pages/KycWizardPage'))
const SearchVehiclesPage = lazy(() => import('@/features/vehicles/pages/SearchVehiclesPage'))
const HomePage = lazy(() => import('@/features/home/pages/HomePage'))
const VehicleDetailPage = lazy(() => import('@/features/vehicles/pages/VehicleDetailPage'))
const MyBookingsPage   = lazy(() => import('@/features/bookings/pages/MyBookingsPage'))

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
  // ── Public Auth Pages ───────────────────────────────────────────────────────
  // ── Root & Admin Desktop Dashboard ──────────────────────────────────────────
  {
    element: <AdminRoute />,
    children: [
      {
        element: <DashboardLayout />,
        children: [
          { path: ROUTES.DASHBOARD, element: wrap(DashboardPage) },
          { path: ROUTES.USERS,     element: wrap(UsersPage) },
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
      { path: ROUTES.LOGIN, element: wrap(LoginPage) },
      { path: ROUTES.REGISTER, element: wrap(RegisterPage) },
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

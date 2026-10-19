import { lazy, Suspense } from 'react'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { ROUTES } from '@/shared/constants'
import { ProtectedRoute, AdminRoute, CustomerRoute } from './ProtectedRoute'
import { DashboardLayout } from '@/app/layouts/DashboardLayout'
import { MobileLayout } from '@/app/layouts/MobileLayout'

// Lazy loaded pages
const LoginPage     = lazy(() => import('@/features/auth/pages/LoginPage'))
const RegisterPage  = lazy(() => import('@/features/auth/pages/RegisterPage'))
const DashboardPage = lazy(() => import('@/features/dashboard/pages/DashboardPage'))
const UsersPage     = lazy(() => import('@/features/users/pages/UsersPage'))
const NotFoundPage  = lazy(() => import('@/features/not-found/pages/NotFoundPage'))
const ProfilePage   = lazy(() => import('@/features/profile/pages/ProfilePage'))
const KycWizardPage = lazy(() => import('@/features/profile/pages/KycWizardPage'))

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
  // ── Public ──────────────────────────────────────────────────────────────────
  {
    path: ROUTES.LOGIN,
    element: wrap(LoginPage),
  },
  {
    path: ROUTES.REGISTER,
    element: wrap(RegisterPage),
  },

  // ── All authenticated users ─────────────────────────────────────────────────
  {
    element: <ProtectedRoute />,
    children: [

      // ── ADMIN / MANAGER / STAFF → Desktop sidebar layout ──────────────────
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

      // ── CUSTOMER → Mobile PWA layout ──────────────────────────────────────
      {
        element: <CustomerRoute />,
        children: [
          {
            element: <MobileLayout />,
            children: [
              { path: ROUTES.PROFILE, element: wrap(ProfilePage) },
            ],
          },
          // KYC wizard: full-screen, no bottom nav
          {
            path: ROUTES.PROFILE_VERIFY,
            element: wrap(KycWizardPage),
          },
        ],
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

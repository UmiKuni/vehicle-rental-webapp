import { NavLink, Outlet, Link } from 'react-router-dom'
import { LayoutDashboard, User, ArrowLeft, Car, House } from 'lucide-react'
import { ROUTES } from '@/shared/constants'
import { cn } from '@/shared/utils'
import { useKycStatus } from '@/features/profile/hooks/useProfile'
import { useAuth } from '@/shared/contexts/AuthContext'
import { ADMIN_ROLES, CUSTOMER_ROLES } from '@/types/auth.types'

function BottomNav() {
  const { user } = useAuth()
  const { data: kyc } = useKycStatus(!!user)
  const isUnverified = user && (!kyc || kyc.status === 'UNVERIFIED')
  const isCustomer = !user || CUSTOMER_ROLES.includes(user.role)

  const navItems = isCustomer
    ? [
        { to: ROUTES.HOME, icon: House, label: 'Home' },
        { to: ROUTES.SEARCH, icon: Car, label: 'Search' },
        { to: user ? ROUTES.PROFILE : ROUTES.PROFILE_AUTH, icon: User, label: 'Profile' },
      ]
    : [
        { to: ROUTES.DASHBOARD, icon: LayoutDashboard, label: 'Dashboard' },
        { to: ROUTES.HOME, icon: House, label: 'Home' },
        { to: ROUTES.SEARCH, icon: Car, label: 'Search' },
        { to: ROUTES.PROFILE, icon: User, label: 'Profile' },
      ]

  return (
    <nav aria-label="Primary navigation" className="app-bottom-nav fixed bottom-0 inset-x-0 z-50 border-t border-border bg-background/95 backdrop-blur-xl safe-area-pb">
      <div className="flex items-stretch max-w-md mx-auto">
        {navItems.map(({ to, icon: Icon, label }) => (
          <NavLink
            key={to}
            to={to}
              end={to === ROUTES.HOME || to === ROUTES.DASHBOARD || to === ROUTES.PROFILE}
            className={({ isActive }) =>
              cn(
                'flex-1 min-h-[50px] flex flex-col items-center justify-center gap-1 py-1.5 relative transition-colors active-press',
                isActive
                  ? 'text-[#1a8a9d]'
                  : 'text-muted-foreground hover:text-foreground'
              )
            }
          >
            {({ isActive }) => (
              <>
                <div className="relative">
                  <Icon className={cn('size-5 transition-transform', isActive && 'scale-110')} />
                  {/* Unverified dot on profile */}
                  {to === ROUTES.PROFILE && isUnverified && (
                    <span className="absolute -top-0.5 -right-0.5 h-2 w-2 rounded-full bg-amber-500 border border-background" />
                  )}
                </div>
                <span className={cn('text-[10px] font-medium transition-all', isActive && 'font-bold text-[#1a8a9d]')}>
                  {label}
                </span>
                {isActive && (
                  <span className="absolute top-0 inset-x-4 h-0.5 rounded-full bg-[#1a8a9d]" />
                )}
              </>
            )}
          </NavLink>
        ))}
      </div>
    </nav>
  )
}

/**
 * Mobile-first PWA layout with sticky bottom navigation.
 * Formatted with a clean mobile viewport container on desktop.
 */
export function MobileLayout() {
  const { user } = useAuth()
  const isAdmin = user && ADMIN_ROLES.includes(user.role)

  return (
    <div className="min-h-screen bg-slate-100/70 dark:bg-zinc-950 flex flex-col">
      {/* Admin Preview Header */}
      {isAdmin && (
        <header className="bg-zinc-900 border-b border-zinc-800 text-white px-4 py-2 sticky top-0 z-40 text-xs">
          <div className="max-w-md mx-auto flex items-center justify-between">
            <span className="flex items-center gap-1.5 text-zinc-300 font-medium">
              <span className="size-2 rounded-full bg-cyan-400 animate-pulse" />
              PWA View Mode ({user.role})
            </span>
            <Link
              to={ROUTES.DASHBOARD}
              className="inline-flex items-center gap-1 text-cyan-400 hover:text-cyan-300 font-semibold"
            >
              <ArrowLeft className="size-3" />
              Back to Dashboard
            </Link>
          </div>
        </header>
      )}

      {/* Main mobile viewport */}
      <main className="w-full max-w-md mx-auto bg-background min-h-screen sm:border-x sm:border-border sm:shadow-sm pb-24 flex-1">
        <Outlet />
      </main>

      <BottomNav />
    </div>
  )
}

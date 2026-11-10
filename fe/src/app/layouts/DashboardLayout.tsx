import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { ArrowLeft, CarFront, ChevronRight, Layers3, LayoutDashboard, LogOut, ShieldCheck, Users } from 'lucide-react'
import { useAuth } from '@/shared/contexts/AuthContext'
import { getInitials } from '@/shared/utils'
import { APP_NAME, ROUTES } from '@/shared/constants'
import { cn } from '@/shared/utils'

const navigation = [
  {
    label: 'WORKSPACE',
    items: [{ to: ROUTES.DASHBOARD, label: 'Overview', icon: LayoutDashboard, end: true }],
  },
  {
    label: 'FLEET OPERATIONS',
    items: [
      { to: ROUTES.ADMIN_VEHICLES, label: 'Fleet vehicles', icon: CarFront, end: false },
      { to: ROUTES.ADMIN_CATALOG, label: 'Vehicle catalog', icon: Layers3, end: false },
    ],
  },
  {
    label: 'ACCESS MANAGEMENT',
    items: [{ to: ROUTES.USERS, label: 'User access', icon: Users, end: false }],
  },
]

const pageNames: Record<string, string> = {
  [ROUTES.DASHBOARD]: 'Overview',
  [ROUTES.ADMIN_VEHICLES]: 'Fleet vehicles',
  [ROUTES.ADMIN_CATALOG]: 'Vehicle catalog',
  [ROUTES.USERS]: 'User access',
}

export function DashboardLayout() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const pageName = pageNames[location.pathname] ?? 'Admin workspace'

  return (
    <div className="admin-workspace min-h-dvh bg-[#f4f6f7] text-[#172321] lg:flex">
      <aside className="flex shrink-0 flex-col bg-[#101719] text-white lg:fixed lg:inset-y-0 lg:w-[264px]">
        <Link to={ROUTES.DASHBOARD} className="flex h-[68px] items-center gap-3 border-b border-white/[.08] px-4 lg:h-[82px] lg:px-6">
          <span className="grid size-10 place-items-center rounded-md bg-[#287f7e] text-white"><CarFront size={20} strokeWidth={2} /></span>
          <span className="min-w-0 text-[15px] font-extrabold tracking-[-.035em] text-white">{APP_NAME}<small className="mt-1 block text-[9px] font-bold tracking-[.2em] text-[#6f8584]">ADMIN CONSOLE</small></span>
        </Link>

        <nav aria-label="Admin navigation" className="flex gap-2 overflow-x-auto px-3 py-2 lg:grid lg:gap-7 lg:overflow-visible lg:px-4 lg:py-7">
          {navigation.map(group => (
            <section key={group.label} className="shrink-0 lg:shrink">
              <p className="mb-2.5 hidden px-3 text-[10px] font-extrabold tracking-[.14em] text-[#1db0ba] lg:block">{group.label}</p>
              <div className="flex gap-1 lg:grid">
                {group.items.map(({ to, label, icon: Icon, end }) => (
                  <NavLink key={to} to={to} end={end} className={({ isActive }) => cn(
                    'group relative flex min-h-[40px] shrink-0 items-center gap-2 rounded-lg px-2.5 text-[12px] font-bold transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#38c4c7] lg:min-h-[46px] lg:shrink lg:gap-3 lg:px-3 lg:text-[14px]',
                    isActive ? 'bg-[#1b2c2e] text-white shadow-[inset_3px_0_0_#20b2ba]' : 'text-[#9eafaf] hover:bg-white/[.055] hover:text-white'
                  )}>
                    <Icon size={19} strokeWidth={2} className="shrink-0" />
                    <span>{label}</span>
                    <ChevronRight className="ml-auto opacity-0 transition group-hover:translate-x-0.5 group-hover:opacity-60" size={15} />
                  </NavLink>
                ))}
              </div>
            </section>
          ))}
        </nav>

        <div className="mt-auto border-t border-white/[.08] p-2 lg:p-4">
          <p className="mb-3 hidden items-center gap-2 border-b border-white/[.08] pb-4 text-[11px] text-[#91a4a2] lg:flex"><ShieldCheck size={15} className="text-[#8fb8b4]" /> Changes apply to the live platform</p>
          <div className="flex items-center gap-3 border-t border-white/[.08] px-1 pt-4">
            <span className="grid size-10 shrink-0 place-items-center rounded-md bg-[#31504d] text-sm font-bold text-white">{getInitials(user?.fullName ?? 'Admin')}</span>
            <div className="min-w-0 flex-1"><p className="m-0 truncate text-[13px] font-bold text-white">{user?.fullName ?? 'Administrator'}</p><p className="mb-0 mt-0.5 text-[11px] font-medium text-[#839494]">{user?.role ?? 'ADMIN'}</p></div>
            <button onClick={logout} title="Sign out" aria-label="Sign out" className="grid size-9 shrink-0 place-items-center rounded-lg text-[#91a2a2] transition hover:bg-red-500/10 hover:text-red-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#38c4c7]"><LogOut size={17} /></button>
          </div>
        </div>
      </aside>

      <section className="min-w-0 flex-1 lg:ml-[264px]">
        <header className="sticky top-0 z-20 flex min-h-[74px] items-center justify-between gap-3 border-b border-[#e3e9e9] bg-white/95 px-4 backdrop-blur md:px-8">
          <div className="min-w-0"><p className="m-0 text-[10px] font-bold uppercase tracking-[.15em] text-[#82908f]">Administration <span className="px-1.5 text-[#bbc4c3]">/</span><span className="font-extrabold text-[#4b5c5a]">{pageName}</span></p><p className="mb-0 mt-1 text-[15px] font-extrabold tracking-[-.02em] text-[#1c2b29]">{pageName}</p></div>
          <div className="flex shrink-0 items-center gap-2 sm:gap-4">
            <Link to={ROUTES.SEARCH} className="inline-flex h-10 items-center gap-2 rounded-lg border border-[#dce5e4] bg-white px-3.5 text-xs font-bold text-[#405553] transition hover:border-[#9dc9c8] hover:bg-[#f4fafa] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#1ca5ad]">
              <ArrowLeft size={15} /><span className="hidden sm:inline">User portal</span><span className="sm:hidden">User</span>
            </Link>
            <span className="hidden h-8 w-px bg-[#e6ebea] sm:block" />
            <div className="hidden min-w-0 sm:block"><p className="m-0 max-w-44 truncate text-xs font-bold text-[#273633]">{user?.fullName}</p><p className="mb-0 mt-0.5 text-[10px] font-semibold text-[#82908f]">Administrator</p></div>
            <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-[#e3f2f1] text-xs font-extrabold text-[#167b7d] sm:hidden">{getInitials(user?.fullName ?? 'Admin')}</span>
          </div>
        </header>
        <main className="mx-auto w-full max-w-[1560px] px-4 pb-8 pt-4 md:px-8 md:pb-9 md:pt-5 xl:px-10"><Outlet /></main>
      </section>
    </div>
  )
}

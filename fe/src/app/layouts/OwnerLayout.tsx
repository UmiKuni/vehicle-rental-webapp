import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { CalendarClock, CarFront, LayoutDashboard, LogOut } from 'lucide-react'
import { useAuth } from '@/shared/contexts/AuthContext'
import { APP_NAME, ROUTES } from '@/shared/constants'

const navigation = [
  { to: ROUTES.OWNER_DASHBOARD, label: 'Overview', icon: LayoutDashboard },
  { to: ROUTES.OWNER_BOOKINGS, label: 'Booking requests', icon: CalendarClock },
  { to: ROUTES.OWNER_VEHICLES, label: 'My vehicles', icon: CarFront },
]

const titles: Record<string, string> = {
  [ROUTES.OWNER_DASHBOARD]: 'Overview',
  [ROUTES.OWNER_BOOKINGS]: 'Booking requests',
  [ROUTES.OWNER_VEHICLES]: 'My vehicles',
}

export function OwnerLayout() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const title = titles[location.pathname] ?? 'Owner workspace'

  return (
    <div className="min-h-dvh bg-[#f4f7f6] text-[#1b2a28] lg:flex">
      <aside className="bg-[#132421] text-white lg:fixed lg:inset-y-0 lg:flex lg:w-[260px] lg:flex-col">
        <Link to={ROUTES.OWNER_DASHBOARD} className="flex h-[68px] items-center gap-3 border-b border-white/10 px-5">
          <span className="grid size-10 place-items-center rounded-xl bg-[#2a8076]"><CarFront size={20} /></span>
          <span className="text-sm font-extrabold tracking-tight">{APP_NAME}<small className="mt-1 block text-[9px] font-bold tracking-[.2em] text-[#9ab6ae]">OWNER WORKSPACE</small></span>
        </Link>
        <nav aria-label="Owner navigation" className="flex gap-2 overflow-x-auto p-3 lg:grid lg:content-start lg:gap-2 lg:p-5">
          {navigation.map(({ to, label, icon: Icon }) => <NavLink key={to} to={to} end={to === ROUTES.OWNER_DASHBOARD} className={({ isActive }) => `flex min-h-11 shrink-0 items-center gap-3 rounded-xl px-3 text-sm font-semibold transition ${isActive ? 'bg-white/10 text-white' : 'text-[#b3c7c1] hover:bg-white/5 hover:text-white'}`}>
            <Icon size={18} />{label}
          </NavLink>)}
        </nav>
        <div className="hidden border-t border-white/10 p-5 lg:mt-auto lg:block">
          <Link to={ROUTES.PROFILE} className="mb-4 block text-xs font-semibold text-[#b3c7c1] hover:text-white">Account profile</Link>
          <div className="flex items-center gap-3">
            <span className="grid size-10 place-items-center rounded-lg bg-[#2c4741] text-sm font-bold">{(user?.fullName ?? 'Owner').slice(0, 1).toUpperCase()}</span>
            <div className="min-w-0 flex-1"><p className="truncate text-xs font-bold">{user?.fullName ?? 'Vehicle owner'}</p><p className="mt-1 text-[10px] text-[#a4b8b2]">OWNER</p></div>
            <button type="button" onClick={() => void logout()} aria-label="Sign out" title="Sign out" className="grid size-9 place-items-center rounded-lg text-[#b3c7c1] hover:bg-white/10 hover:text-white"><LogOut size={17} /></button>
          </div>
        </div>
      </aside>

      <main className="min-w-0 flex-1 lg:ml-[260px]">
        <header className="sticky top-0 z-20 flex min-h-[68px] items-center justify-between border-b border-[#e1e9e5] bg-white/95 px-4 backdrop-blur sm:px-7">
          <div><p className="m-0 text-[10px] font-bold uppercase tracking-[.14em] text-[#7e9189]">Owner workspace / {title}</p><h1 className="mb-0 mt-1 text-base font-extrabold">{title}</h1></div>
          <div className="flex items-center gap-3"><Link to={ROUTES.PROFILE} className="text-xs font-semibold text-[#49635c] hover:text-[#1c7468]">{user?.fullName ?? 'Account'}</Link><button type="button" onClick={() => void logout()} aria-label="Sign out" className="grid size-9 place-items-center rounded-lg border border-[#dfe8e3] text-[#49635c] hover:bg-[#f3f7f5] lg:hidden"><LogOut size={16} /></button></div>
        </header>
        <section className="mx-auto max-w-6xl p-4 pb-10 sm:p-7"><Outlet /></section>
      </main>
    </div>
  )
}

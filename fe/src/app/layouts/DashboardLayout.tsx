import { useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  LayoutDashboard,
  Users,
  Car,
  CalendarCheck,
  BarChart3,
  Settings,
  LogOut,
  Menu,
  X,
  Moon,
  Sun,
  Bell,
  Search,
  Command,
} from 'lucide-react'
import { useAuth } from '@/shared/contexts/AuthContext'
import { useTheme } from '@/shared/contexts/ThemeContext'
import { cn } from '@/shared/utils'
import { getInitials } from '@/shared/utils'
import { ROUTES, APP_NAME } from '@/shared/constants'

interface NavSection {
  title: string
  items: {
    to: string
    icon: typeof LayoutDashboard
    label: string
    badge?: string
    disabled?: boolean
  }[]
}

const navSections: NavSection[] = [
  {
    title: 'Vận hành',
    items: [
      { to: ROUTES.DASHBOARD, icon: LayoutDashboard, label: 'Tổng quan' },
      { to: '/vehicles', icon: Car, label: 'Phương tiện', badge: '64 xe', disabled: true },
      { to: '/rentals', icon: CalendarCheck, label: 'Đơn thuê', badge: '18 hoạt động', disabled: true },
    ],
  },
  {
    title: 'Hệ thống',
    items: [
      { to: ROUTES.USERS, icon: Users, label: 'Người dùng' },
      { to: '/analytics', icon: BarChart3, label: 'Báo cáo', disabled: true },
      { to: '/settings', icon: Settings, label: 'Cài đặt', disabled: true },
    ],
  },
]

export function DashboardLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const { user, logout } = useAuth()
  const { resolvedTheme, setTheme } = useTheme()
  const location = useLocation()

  const toggleTheme = () => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')

  return (
    <div className="flex h-screen overflow-hidden bg-background">
      {/* Mobile Backdrop */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-30 bg-black/60 backdrop-blur-xs lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex w-64 flex-col bg-sidebar border-r border-border',
          'transition-transform duration-250 ease-out lg:relative lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        )}
      >
        {/* Workspace Brand Header */}
        <div className="flex h-16 items-center gap-3 border-b border-border px-5">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary text-primary-foreground shadow-xs">
            <Car className="size-5" />
          </div>
          <div className="min-w-0 flex-1">
            <Link to={ROUTES.DASHBOARD} className="flex items-center gap-1.5 font-bold tracking-tight text-foreground text-sm">
              {APP_NAME}
            </Link>
            <div className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
              <span className="size-1.5 rounded-full bg-emerald-500 animate-pulse" />
              <span>Hub Trung tâm • Trực tuyến</span>
            </div>
          </div>
          <button
            onClick={() => setSidebarOpen(false)}
            className="rounded-md p-1.5 text-muted-foreground hover:bg-accent lg:hidden"
          >
            <X className="size-4" />
          </button>
        </div>

        {/* Categorized Nav Links */}
        <nav className="flex-1 space-y-5 overflow-y-auto px-3 py-4">
          {navSections.map((section) => (
            <div key={section.title} className="space-y-1">
              <p className="px-3 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground/80">
                {section.title}
              </p>
              {section.items.map(({ to, icon: Icon, label, badge, disabled }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={to === ROUTES.DASHBOARD}
                  onClick={() => setSidebarOpen(false)}
                  className={({ isActive }) =>
                    cn(
                      'group flex items-center gap-2.5 rounded-lg px-3 py-2 text-xs font-medium transition-all active-press',
                      disabled && 'opacity-40 cursor-not-allowed',
                      isActive && !disabled
                        ? 'bg-primary text-primary-foreground font-semibold shadow-xs'
                        : 'text-muted-foreground hover:bg-accent hover:text-foreground'
                    )
                  }
                >
                  <Icon className="size-4 shrink-0 transition-transform group-hover:scale-105" />
                  <span className="truncate">{label}</span>
                  {badge && (
                    <span
                      className={cn(
                        'ml-auto rounded-md px-1.5 py-0.5 text-[10px] font-mono tracking-tight',
                        disabled ? 'bg-muted text-muted-foreground' : 'bg-primary/20 text-primary-foreground'
                      )}
                    >
                      {badge}
                    </span>
                  )}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        {/* Fleet Readiness Mini Progress */}
        <div className="border-t border-border p-3.5 mx-2 mb-2 rounded-lg bg-muted/40">
          <div className="flex items-center justify-between text-[11px] text-muted-foreground mb-1.5 font-medium">
            <span>Tỷ lệ sẵn sàng</span>
            <span className="font-mono text-foreground font-semibold">84.5%</span>
          </div>
          <div className="h-1.5 w-full rounded-full bg-border overflow-hidden">
            <div className="h-full rounded-full bg-emerald-500 w-[84.5%]" />
          </div>
        </div>

        {/* User Footer */}
        <div className="border-t border-border p-3.5">
          <div className="flex items-center gap-3">
            <div className="relative flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-zinc-800 text-xs font-bold text-white font-mono">
              {getInitials(user?.fullName ?? 'U')}
              <span className="absolute bottom-0 right-0 size-2 rounded-full border-2 border-sidebar bg-emerald-500" />
            </div>
            <div className="min-w-0 flex-1">
              <p className="truncate text-xs font-semibold text-foreground">{user?.fullName}</p>
              <p className="truncate text-[11px] text-muted-foreground font-mono">@{user?.username} • {user?.role}</p>
            </div>
            <button
              onClick={logout}
              title="Đăng xuất"
              className="rounded-md p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors"
            >
              <LogOut className="size-4" />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Area */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* Top Navbar */}
        <header className="flex h-16 shrink-0 items-center justify-between gap-4 border-b border-border bg-background/95 backdrop-blur px-4 lg:px-6">
          <div className="flex items-center gap-3">
            <button
              onClick={() => setSidebarOpen(true)}
              className="rounded-md p-2 text-muted-foreground hover:bg-accent lg:hidden"
              aria-label="Mở menu"
            >
              <Menu className="size-5" />
            </button>

            {/* Breadcrumb / Page Title */}
            <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
              <span>Hệ thống</span>
              <span>/</span>
              <span className="font-semibold text-foreground text-sm">
                {getPageTitle(location.pathname)}
              </span>
            </div>
          </div>

          <div className="flex items-center gap-2.5">
            {/* Quick search shortcut trigger */}
            <div className="hidden items-center gap-2 rounded-md border border-border bg-muted/30 px-3 py-1.5 text-xs text-muted-foreground md:flex">
              <Search className="size-3.5 text-muted-foreground" />
              <span>Tìm kiếm nhanh...</span>
              <kbd className="flex items-center gap-0.5 rounded border border-border bg-muted px-1.5 py-0.2 text-[10px] font-mono text-muted-foreground">
                <Command className="size-2.5" /> K
              </kbd>
            </div>

            {/* Notifications */}
            <button
              className="relative rounded-md p-2 text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
              title="Thông báo"
            >
              <Bell className="size-4" />
              <span className="absolute right-1.5 top-1.5 h-1.5 w-1.5 rounded-full bg-blue-500 ring-2 ring-background" />
            </button>

            {/* Theme Toggle */}
            <button
              onClick={toggleTheme}
              className="rounded-md p-2 text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
              aria-label="Đổi giao diện"
              title="Chuyển chế độ sáng/tối"
            >
              {resolvedTheme === 'dark' ? <Sun className="size-4" /> : <Moon className="size-4" />}
            </button>
          </div>
        </header>

        {/* Page Content */}
        <main className="flex-1 overflow-y-auto">
          <div className="p-4 lg:p-6 max-w-7xl mx-auto">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}

function getPageTitle(pathname: string): string {
  const map: Record<string, string> = {
    '/': 'Tổng quan vận hành',
    '/users': 'Quản lý tài khoản',
    '/vehicles': 'Quản lý phương tiện',
  }
  return map[pathname] ?? 'Hệ thống'
}

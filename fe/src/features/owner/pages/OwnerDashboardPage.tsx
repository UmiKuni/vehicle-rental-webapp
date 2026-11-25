import { useQuery } from '@tanstack/react-query'
import { ArrowRight, CalendarClock, CarFront, CircleDollarSign, Clock3 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useAuth } from '@/shared/contexts/AuthContext'
import { ROUTES } from '@/shared/constants'
import { ownerApi } from '../api/owner.api'

const money = (value: number) => `${new Intl.NumberFormat('vi-VN').format(value)} ₫`

export default function OwnerDashboardPage() {
  const { user } = useAuth()
  const now = new Date()
  const bookings = useQuery({ queryKey: ['owner', 'bookings', 'recent'], queryFn: () => ownerApi.getBookings(1, 5) })
  const pendingBookings = useQuery({ queryKey: ['owner', 'bookings', 'pending-count'], queryFn: () => ownerApi.getBookings(1, 1, 'Pending') })
  const vehicles = useQuery({ queryKey: ['owner', 'vehicles', user?.id], queryFn: () => ownerApi.getVehicles(String(user!.id)), enabled: !!user?.id })
  const revenue = useQuery({ queryKey: ['owner', 'revenue', now.getFullYear(), now.getMonth() + 1], queryFn: () => ownerApi.getRevenue(now.getMonth() + 1, now.getFullYear()), enabled: user?.role === 'OWNER' })
  const pendingCount = pendingBookings.data?.totalCount ?? 0
  const activeCount = vehicles.data?.filter((vehicle) => vehicle.status.toUpperCase() === 'PUBLISHED').length ?? 0
  const hasError = bookings.isError || pendingBookings.isError || vehicles.isError || revenue.isError

  return <div className="space-y-6">
    <div><p className="mb-1 text-xs font-bold uppercase tracking-[.16em] text-[#218276]">OWNER OPERATIONS</p><h2 className="m-0 text-2xl font-extrabold tracking-tight">Welcome, {user?.fullName ?? 'Owner'}</h2><p className="mb-0 mt-1 text-sm text-[#6b7e77]">A dedicated workspace for your vehicles, renter requests, and earnings.</p></div>
    {hasError && <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">Some owner data could not be loaded. Use the section links to retry each area.</div>}
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <Metric icon={CalendarClock} label="Pending requests" value={pendingBookings.isPending ? '…' : String(pendingCount)} href={ROUTES.OWNER_BOOKINGS} />
      <Metric icon={CarFront} label="Published vehicles" value={vehicles.isPending ? '…' : String(activeCount)} href={ROUTES.OWNER_VEHICLES} />
      <Metric icon={CircleDollarSign} label="Net earnings this month" value={revenue.isPending ? '…' : money(revenue.data?.netEarnings ?? 0)} href={ROUTES.OWNER_BOOKINGS} />
      <Metric icon={Clock3} label="Trips this month" value={revenue.isPending ? '…' : String(revenue.data?.totalTrips ?? 0)} href={ROUTES.OWNER_BOOKINGS} />
    </div>

    <section className="overflow-hidden rounded-2xl border border-[#e0e9e4] bg-white">
      <div className="flex items-center justify-between gap-3 border-b border-[#e8efeb] px-4 py-4 sm:px-5"><div><h3 className="m-0 text-sm font-extrabold">Latest booking requests</h3><p className="mb-0 mt-1 text-xs text-[#76877f]">Requests for vehicles linked to your owner account.</p></div><Link to={ROUTES.OWNER_BOOKINGS} className="inline-flex items-center gap-1 text-xs font-bold text-[#21776e]">View all <ArrowRight size={14} /></Link></div>
      {bookings.isPending ? <div className="p-5 text-sm text-[#74847d]">Loading requests…</div> : bookings.isError ? <button type="button" onClick={() => void bookings.refetch()} className="p-5 text-sm font-semibold text-red-700">Could not load requests. Retry</button> : bookings.data.items.length === 0 ? <p className="p-5 text-sm text-[#74847d]">No booking requests yet.</p> : <div className="divide-y divide-[#edf1ee]">{bookings.data.items.map((booking) => <Link key={booking.id} to={ROUTES.OWNER_BOOKINGS} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3.5 hover:bg-[#f8faf9] sm:px-5"><div className="min-w-0"><p className="m-0 truncate text-sm font-bold">{booking.vehicleModel || booking.vehiclePlate || 'Vehicle booking'}</p><p className="mb-0 mt-1 text-xs text-[#77877f]">{new Date(booking.startTime).toLocaleDateString()} – {new Date(booking.endTime).toLocaleDateString()} · {booking.vehiclePlate}</p></div><div className="flex items-center gap-3"><span className="text-xs font-bold">{money(booking.totalAmount)}</span><Status status={booking.status} /></div></Link>)}</div>}
    </section>

    <div className="grid gap-3 sm:grid-cols-2">
      <QuickLink to={ROUTES.OWNER_VEHICLES} title="Manage your fleet" description="Review each vehicle’s listing and publication status." icon={CarFront} />
      <QuickLink to={ROUTES.OWNER_BOOKINGS} title="Respond to renters" description="Approve or reject incoming booking requests." icon={CalendarClock} />
    </div>
  </div>
}

function Metric({ icon: Icon, label, value, href }: { icon: typeof CarFront; label: string; value: string; href: string }) {
  return <Link to={href} className="rounded-2xl border border-[#e0e9e4] bg-white p-4 transition hover:border-[#a8c9bd] hover:shadow-sm"><span className="grid size-9 place-items-center rounded-xl bg-[#e9f4ef] text-[#247a6d]"><Icon size={18} /></span><p className="mb-1 mt-4 text-xs font-semibold text-[#71817a]">{label}</p><p className="m-0 truncate text-xl font-extrabold tracking-tight">{value}</p></Link>
}

function QuickLink({ to, title, description, icon: Icon }: { to: string; title: string; description: string; icon: typeof CarFront }) {
  return <Link to={to} className="flex items-center gap-3 rounded-2xl border border-[#e0e9e4] bg-white p-4 hover:border-[#a8c9bd]"><span className="grid size-10 shrink-0 place-items-center rounded-xl bg-[#e9f4ef] text-[#247a6d]"><Icon size={19} /></span><span className="min-w-0 flex-1"><strong className="block text-sm">{title}</strong><span className="mt-1 block text-xs leading-5 text-[#71817a]">{description}</span></span><ArrowRight size={16} className="text-[#247a6d]" /></Link>
}

export function Status({ status }: { status: string }) {
  const normalized = status.toUpperCase()
  const tone = normalized === 'PENDING' || normalized === 'PENDINGAPPROVAL' ? 'bg-amber-100 text-amber-800' : normalized === 'PUBLISHED' || normalized === 'CONFIRMED' || normalized === 'COMPLETED' ? 'bg-emerald-100 text-emerald-800' : normalized === 'REJECTED' || normalized === 'CANCELLED' ? 'bg-red-100 text-red-800' : 'bg-slate-100 text-slate-700'
  const labels: Record<string, string> = { PENDINGAPPROVAL: 'Pending approval', INPROGRESS: 'In progress' }
  const label = labels[normalized] ?? normalized.replaceAll('_', ' ')
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-[10px] font-bold ${tone}`}>{label}</span>
}

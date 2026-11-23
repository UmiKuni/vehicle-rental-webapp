import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, ChevronLeft, ChevronRight, X } from 'lucide-react'
import { toast } from 'sonner'
import { ownerApi } from '../api/owner.api'
import type { OwnerBooking } from '../api/owner.api'
import { Status } from './OwnerDashboardPage'
import { getErrorMessage } from '@/shared/lib/query-client'

const pageSize = 20
const statusOptions = [
  ['', 'All statuses'], ['Pending', 'Pending'], ['Confirmed', 'Confirmed'], ['Paid', 'Paid'],
  ['InProgress', 'In progress'], ['Completed', 'Completed'], ['Cancelled', 'Cancelled'], ['Rejected', 'Rejected'], ['Expired', 'Expired'],
]
const money = (amount: number) => `${new Intl.NumberFormat('vi-VN').format(amount)} ₫`

export default function OwnerBookingsPage() {
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState('')
  const [selected, setSelected] = useState<OwnerBooking | null>(null)
  const bookings = useQuery({ queryKey: ['owner', 'bookings', page, status], queryFn: () => ownerApi.getBookings(page, pageSize, status) })
  const detail = useQuery({ queryKey: ['owner', 'booking', selected?.id], queryFn: () => ownerApi.getBooking(selected!.id), enabled: !!selected })
  const action = useMutation({
    mutationFn: async ({ bookingId, kind, reason }: { bookingId: string; kind: 'approve' | 'reject'; reason?: string }) =>
      kind === 'approve' ? ownerApi.approveBooking(bookingId) : ownerApi.rejectBooking(bookingId, reason ?? ''),
    onSuccess: async (_response, variables) => {
      toast.success(variables.kind === 'approve' ? 'Booking approved. The customer can continue to payment.' : 'Booking request rejected.')
      setSelected(null)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['owner', 'bookings'] }),
        queryClient.invalidateQueries({ queryKey: ['owner', 'booking'] }),
        queryClient.invalidateQueries({ queryKey: ['owner', 'revenue'] }),
      ])
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })
  const rows = bookings.data?.items ?? []
  const totalPages = Math.max(1, bookings.data?.totalPages ?? 1)

  const reject = (bookingId: string) => {
    const reason = window.prompt('Reason for rejecting this booking (required):')?.trim()
    if (!reason) return
    action.mutate({ bookingId, kind: 'reject', reason })
  }
  const selectedStatus = selected?.status.replace(/[_\s]/g, '').toUpperCase()

  return <div className="space-y-5">
    <div className="flex flex-wrap items-end justify-between gap-3"><div><p className="mb-1 text-xs font-bold uppercase tracking-[.16em] text-[#218276]">OWNER BOOKINGS</p><h2 className="m-0 text-2xl font-extrabold tracking-tight">Incoming requests</h2><p className="mb-0 mt-1 text-sm text-[#6b7e77]">Only bookings for vehicles owned by this account are returned by the backend.</p></div><label className="text-xs font-semibold text-[#61746c]">Status<select value={status} onChange={(event) => { setStatus(event.target.value); setPage(1) }} className="ml-2 h-10 rounded-lg border border-[#dce7e1] bg-white px-3 text-sm text-[#253732]">{statusOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label></div>
    <section className="overflow-hidden rounded-2xl border border-[#e0e9e4] bg-white">
      {bookings.isPending ? <p className="p-5 text-sm text-[#71817a]">Loading booking requests…</p> : bookings.isError ? <div className="p-5"><p role="alert" className="text-sm text-red-700">Could not load owner bookings. Check the account role and connection.</p><button type="button" onClick={() => void bookings.refetch()} className="mt-2 text-sm font-bold text-[#21776e]">Retry</button></div> : rows.length === 0 ? <div className="p-10 text-center"><CalendarClock className="mx-auto text-[#90aaa0]" /><p className="mb-0 mt-3 text-sm font-semibold">No booking requests for this filter.</p></div> : <div className="divide-y divide-[#edf1ee]">{rows.map((booking) => <BookingRow key={booking.id} booking={booking} onOpen={() => setSelected(booking)} />)}</div>}
      {!bookings.isPending && !bookings.isError && totalPages > 1 && <div className="flex items-center justify-between border-t border-[#e8efeb] px-4 py-3"><span className="text-xs text-[#71817a]">Page {page} of {totalPages} · {bookings.data?.totalCount ?? 0} bookings</span><div className="flex gap-2"><button type="button" disabled={page <= 1} onClick={() => setPage((value) => Math.max(1, value - 1))} aria-label="Previous page" className="grid size-9 place-items-center rounded-lg border border-[#dce7e1] disabled:opacity-40"><ChevronLeft size={17} /></button><button type="button" disabled={page >= totalPages} onClick={() => setPage((value) => Math.min(totalPages, value + 1))} aria-label="Next page" className="grid size-9 place-items-center rounded-lg border border-[#dce7e1] disabled:opacity-40"><ChevronRight size={17} /></button></div></div>}
    </section>

    {selected && <div className="fixed inset-0 z-50 grid place-items-center bg-black/45 p-4" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget && !action.isPending) setSelected(null) }}>
      <section role="dialog" aria-modal="true" aria-labelledby="owner-booking-title" className="max-h-[90dvh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-5 shadow-2xl sm:p-6">
        <div className="flex items-start justify-between gap-4"><div><p className="mb-1 text-xs font-bold uppercase tracking-[.15em] text-[#218276]">BOOKING REQUEST</p><h3 id="owner-booking-title" className="m-0 text-xl font-extrabold">{selected.vehicleModel || 'Vehicle'} <span className="text-sm font-semibold text-[#74847d]">{selected.vehiclePlate}</span></h3></div><button type="button" onClick={() => setSelected(null)} disabled={action.isPending} aria-label="Close booking details" className="grid size-9 place-items-center rounded-lg border border-[#dce7e1]"><X size={17} /></button></div>
        {detail.isPending ? <p className="py-8 text-sm text-[#71817a]">Loading booking and renter details…</p> : detail.isError ? <div className="py-5"><p role="alert" className="text-sm text-red-700">Could not load booking details.</p><button type="button" onClick={() => void detail.refetch()} className="mt-2 text-sm font-bold text-[#21776e]">Retry</button></div> : detail.data && <>
          <div className="mt-5 grid grid-cols-2 gap-3 rounded-xl bg-[#f5f8f6] p-4 text-sm"><Info label="Renter" value={detail.data.customerName || 'Not provided'} /><Info label="Phone" value={detail.data.customerPhone || 'Not provided'} /><Info label="Pickup" value={new Date(detail.data.startTime).toLocaleString()} /><Info label="Return" value={new Date(detail.data.endTime).toLocaleString()} /><Info label="Pickup location" value={detail.data.pickupLocation || 'Not provided'} /><Info label="Status" value={detail.data.status} /><Info label="Rental" value={money(detail.data.totalRentAmount)} /><Info label="Security deposit" value={money(detail.data.securityDepositAmount)} /></div>
          {selected.status.toUpperCase() === 'PENDING' && <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end"><button type="button" disabled={action.isPending} onClick={() => reject(selected.id)} className="h-10 rounded-lg border border-red-200 px-4 text-sm font-bold text-red-700 disabled:opacity-50">Reject request</button><button type="button" disabled={action.isPending} onClick={() => action.mutate({ bookingId: selected.id, kind: 'approve' })} className="h-10 rounded-lg bg-[#20786d] px-4 text-sm font-bold text-white disabled:opacity-50">{action.isPending ? 'Saving…' : 'Approve booking'}</button></div>}
          {['PAID', 'INPROGRESS'].includes(selectedStatus ?? '') && <div className="mt-5 flex gap-2">{selectedStatus === 'PAID' && <Link to={`/owner/bookings/${selected.id}/pickup`} onClick={() => setSelected(null)} className="flex h-10 flex-1 items-center justify-center rounded-lg bg-[#20786d] text-sm font-bold text-white">Start pickup handover</Link>}{selectedStatus === 'INPROGRESS' && <Link to={`/owner/bookings/${selected.id}/return`} onClick={() => setSelected(null)} className="flex h-10 flex-1 items-center justify-center rounded-lg bg-[#20786d] text-sm font-bold text-white">Start return handover</Link>}</div>}
          {selectedStatus !== 'PENDING' && <p className="mb-0 mt-4 text-xs text-[#71817a]">This request has already moved past owner approval.</p>}
        </>}
      </section>
    </div>}
  </div>
}

function BookingRow({ booking, onOpen }: { booking: OwnerBooking; onOpen: () => void }) {
  return <button type="button" onClick={onOpen} className="flex w-full flex-wrap items-center justify-between gap-3 px-4 py-4 text-left hover:bg-[#f8faf9] sm:px-5"><span className="min-w-0 flex-1"><span className="block truncate text-sm font-bold">{booking.vehicleModel || 'Vehicle booking'} <span className="font-medium text-[#74847d]">· {booking.vehiclePlate || 'No plate'}</span></span><span className="mt-1 block text-xs text-[#71817a]">{new Date(booking.startTime).toLocaleDateString()} – {new Date(booking.endTime).toLocaleDateString()}</span></span><span className="flex items-center gap-3"><span className="text-xs font-bold">{money(booking.totalAmount)}</span><Status status={booking.status} /></span></button>
}

function Info({ label, value }: { label: string; value: string }) {
  return <div className="min-w-0"><p className="m-0 text-[10px] font-bold uppercase tracking-wide text-[#7b8d85]">{label}</p><p className="mb-0 mt-1 break-words text-xs font-semibold text-[#293b35]">{value}</p></div>
}

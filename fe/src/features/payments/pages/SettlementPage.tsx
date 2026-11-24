import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, CircleDollarSign, Clock3, ShieldCheck } from 'lucide-react'
import { ROUTES } from '@/shared/constants'
import { paymentApi, type BookingPaymentSummary } from '../api/payment.api'

function money(value = 0) { return new Intl.NumberFormat('vi-VN').format(value) + ' ₫' }

export default function SettlementPage() {
  const { bookingId = '' } = useParams()
  const [summary, setSummary] = useState<BookingPaymentSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)

  useEffect(() => {
    let active = true
    paymentApi.getBookingPayment(bookingId).then((value) => { if (active) setSummary(value) }).catch(() => { if (active) setError(true) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [bookingId])

  const deposit = summary?.deposit
  const status = deposit?.status?.toLowerCase() ?? ''
  const released = status === 'released'

  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={`/bookings/${bookingId}`} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Trip details</Link>
      <div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Settlement</p><h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">Deposit refund</h1><p className="mt-1 text-sm text-muted-foreground">Track your deposit and any deductions after vehicle return.</p></div>
      {loading && <div className="h-40 animate-pulse rounded-2xl bg-muted" />}
      {error && <p role="alert" className="rounded-xl border border-destructive/25 bg-destructive/5 p-4 text-sm text-destructive">Unable to load settlement details. The payment API may not be available yet.</p>}
      {!loading && !error && <>
        <section className="rounded-2xl border border-border bg-card p-4"><div className="flex items-center gap-3"><span className="flex size-11 items-center justify-center rounded-xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><CircleDollarSign size={22} /></span><div><p className="text-xs text-muted-foreground">Original deposit</p><p className="mt-0.5 text-xl font-bold text-foreground">{money(deposit?.amount)}</p></div></div><div className="mt-4 grid grid-cols-2 gap-3 border-t border-border pt-4"><div><p className="text-[11px] text-muted-foreground">Deducted</p><p className="mt-1 text-sm font-bold text-foreground">{money(deposit?.deductedAmount)}</p></div><div><p className="text-[11px] text-muted-foreground">Refunded</p><p className="mt-1 text-sm font-bold text-emerald-600">{money(deposit?.refundedAmount)}</p></div></div></section>
        <section className="rounded-2xl border border-border bg-card p-4"><div className="flex items-center gap-2 text-sm font-bold text-foreground"><Clock3 size={17} className="text-[#1a8a9d]" /> Settlement status</div><div className="mt-3 flex items-center justify-between rounded-xl bg-muted/50 p-3"><span className="text-xs text-muted-foreground">Deposit</span><span className={`rounded-full px-2.5 py-1 text-[10px] font-bold ${released ? 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300' : 'bg-amber-500/10 text-amber-700 dark:text-amber-300'}`}>{released ? 'Refunded' : deposit?.status ?? 'Awaiting update'}</span></div>{deposit?.notes && <p className="mt-3 text-xs leading-5 text-muted-foreground">{deposit.notes}</p>}</section>
        <div className="rounded-xl border border-[#1a8a9d]/20 bg-[#1a8a9d]/5 p-3 text-xs leading-5 text-muted-foreground"><ShieldCheck size={15} className="mr-1 inline text-[#1a8a9d]" />The final refund depends on the return inspection and the platform's settlement review.</div>
      </>}
      <Link to={ROUTES.BOOKINGS} className="flex h-11 w-full items-center justify-center rounded-xl border border-border bg-background text-sm font-semibold text-foreground hover:bg-muted">Back to trips</Link>
    </main>
  )
}

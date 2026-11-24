import { useRef } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, CheckCircle2, Clock3, XCircle } from 'lucide-react'
import { paymentApi, type BookingPaymentSummary, type PaymentDetail } from '../api/payment.api'
import { ROUTES } from '@/shared/constants'

function getStatus(data: BookingPaymentSummary | PaymentDetail | undefined) {
  if (!data) return ''
  return 'status' in data ? data.status : data.mainPayment?.status ?? ''
}

export default function PaymentResultPage() {
  const [params] = useSearchParams()
  const bookingId = params.get('bookingId') ?? ''
  const paymentId = params.get('paymentId')
  const hasReference = Boolean(bookingId || paymentId)
  const pollingStartedAt = useRef<number | null>(null)
  const result = useQuery({
    queryKey: ['payment-result', bookingId, paymentId],
    queryFn: () => {
      const now = Date.now()
      if (pollingStartedAt.current === null || now - pollingStartedAt.current >= 120_000) pollingStartedAt.current = now
      return paymentId ? paymentApi.getPayment(paymentId) : paymentApi.getBookingPayment(bookingId)
    },
    enabled: hasReference,
    retry: false,
    refetchInterval: (query) => {
      const status = getStatus(query.state.data)
      const normalized = status.toLowerCase()
      if (['success', 'succeeded', 'paid', 'completed', 'failed', 'cancelled', 'canceled', 'rejected'].includes(normalized)) return false
      return pollingStartedAt.current !== null && Date.now() - pollingStartedAt.current < 120_000 ? 5_000 : false
    },
  })

  const data = result.data
  const summary = data && 'mainPayment' in data ? data : null
  const status = getStatus(data)
  const normalized = status.toLowerCase()
  const success = ['success', 'succeeded', 'paid', 'completed'].includes(normalized)
  const failed = ['failed', 'cancelled', 'canceled', 'rejected'].includes(normalized)
  const hasError = result.isError || !hasReference
  const Icon = hasError || failed ? XCircle : success ? CheckCircle2 : Clock3

  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={ROUTES.BOOKINGS} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Trips</Link>
      <section className="flex flex-col items-center rounded-2xl border border-border bg-card px-5 py-9 text-center">
        <div className={`flex size-16 items-center justify-center rounded-2xl ${hasError || failed ? 'bg-destructive/10 text-destructive' : success ? 'bg-emerald-500/10 text-emerald-600' : 'bg-amber-500/10 text-amber-600'}`}><Icon size={30} /></div>
        <p className="mt-5 text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Payment result</p>
        <h1 className="mt-2 text-xl font-bold text-foreground">{result.isPending ? 'Checking payment status…' : hasError ? 'Unable to retrieve payment status' : success ? 'Payment successful' : failed ? 'Payment unsuccessful' : 'Awaiting confirmation'}</h1>
        <p className="mt-2 max-w-xs text-sm leading-6 text-muted-foreground">{hasError ? 'Could not connect to the payment service. Check the trip details for an update.' : success ? 'The transaction has been recorded. Your trip status will update after the system confirms it.' : failed ? 'The transaction was not completed. You can return and try again.' : 'The payment service is checked automatically for up to two minutes while it confirms the transaction.'}</p>
        {(summary?.mainPayment || status) && <div className="mt-6 w-full rounded-xl bg-muted/50 p-3 text-left text-xs"><div className="flex justify-between"><span className="text-muted-foreground">Booking</span><span className="font-mono font-semibold text-foreground">{bookingId || summary?.bookingId}</span></div>{summary?.mainPayment && <div className="mt-2 flex justify-between"><span className="text-muted-foreground">Amount</span><span className="font-bold text-foreground">{new Intl.NumberFormat('vi-VN').format(summary.mainPayment.amount)} ₫</span></div>}<div className="mt-2 flex justify-between"><span className="text-muted-foreground">Status</span><span className="font-semibold text-foreground">{status || 'Awaiting payment'}</span></div></div>}
        {hasError && <button type="button" onClick={() => void result.refetch()} disabled={result.isFetching || !hasReference} className="mt-5 h-10 w-full rounded-xl border border-border text-sm font-semibold text-foreground disabled:opacity-50">{result.isFetching ? 'Checking…' : 'Try again'}</button>}
        <Link to={bookingId ? `/bookings/${bookingId}` : ROUTES.BOOKINGS} className="mt-6 flex h-11 w-full items-center justify-center rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485]">View trip</Link>
      </section>
    </main>
  )
}

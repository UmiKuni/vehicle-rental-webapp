import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, ArrowRight, Check, CreditCard, LockKeyhole, QrCode, ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { ROUTES } from '@/shared/constants'
import { useBookings } from '@/features/bookings/hooks/useBookings'
import { paymentApi, type PaymentCheckout } from '../api/payment.api'
import { formatDate } from '@/shared/utils'

const methods = [
  { id: 'VNPay', label: 'VNPay', detail: 'ATM cards, bank accounts, or QR' },
  { id: 'Momo', label: 'MoMo', detail: 'Pay with your MoMo wallet' },
  { id: 'BankTransfer', label: 'Bank transfer', detail: 'Scan a bank QR code' },
]

export default function BookingPaymentPage() {
  const { bookingId = '' } = useParams()
  const navigate = useNavigate()
  const { data: bookings = [], isLoading } = useBookings()
  const booking = bookings.find((item) => item.id === bookingId)
  const [method, setMethod] = useState('VNPay')
  const [checkout, setCheckout] = useState<PaymentCheckout | null>(null)
  const [isPending, setIsPending] = useState(false)
  const [error, setError] = useState('')

  const beginCheckout = async () => {
    setIsPending(true)
    setError('')
    try {
      const result = await paymentApi.createCheckout(bookingId, method, `${window.location.origin}/payments/result?bookingId=${encodeURIComponent(bookingId)}`)
      setCheckout(result)
    } catch (requestError) {
      const message = isAxiosError(requestError) && requestError.response?.status === 404
        ? 'The payment service is not available yet. Please try again later.'
        : 'Could not create a payment session. Please try again.'
      setError(message)
      toast.error(message)
    } finally {
      setIsPending(false)
    }
  }

  if (isLoading) return <main className="space-y-4 px-4 pt-5"><div className="h-6 w-40 animate-pulse rounded bg-muted" /><div className="h-40 animate-pulse rounded-2xl bg-muted" /></main>
  if (!booking) return <main className="space-y-4 px-4 pt-5"><Link to={ROUTES.BOOKINGS} className="inline-flex items-center gap-2 text-xs text-muted-foreground"><ArrowLeft size={15} /> Trips</Link><p className="rounded-xl border border-border p-4 text-sm text-muted-foreground">Booking not found.</p></main>

  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={`/bookings/${bookingId}`} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Trip details</Link>
      <div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Payment</p><h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">Complete your booking</h1><p className="mt-1 text-sm text-muted-foreground">Choose a payment method for the rental fee and deposit.</p></div>
      <section className="rounded-2xl border border-border bg-card p-4"><div className="flex items-center justify-between"><div><p className="text-xs text-muted-foreground">Booking code</p><p className="mt-0.5 font-mono text-sm font-bold text-foreground">{booking.bookingCode}</p></div><span className="rounded-full bg-amber-500/10 px-2.5 py-1 text-[10px] font-bold text-amber-700 dark:text-amber-300">Awaiting payment</span></div><div className="mt-4 flex items-center gap-3"><img src={booking.vehicle.imageUrl} alt="" className="size-16 rounded-xl border border-border object-cover" /><div className="min-w-0"><p className="truncate text-sm font-bold text-foreground">{booking.vehicle.name}</p><p className="mt-1 text-xs text-muted-foreground">{formatDate(booking.startDate)} to {formatDate(booking.endDate)}</p></div></div><div className="mt-4 space-y-2 border-t border-border pt-3 text-sm"><div className="flex justify-between text-muted-foreground"><span>Rental fee</span><span>{new Intl.NumberFormat('vi-VN').format(booking.totalAmount)} ₫</span></div><div className="flex justify-between text-muted-foreground"><span>Deposit</span><span>{new Intl.NumberFormat('vi-VN').format(booking.depositAmount)} ₫</span></div><div className="flex justify-between border-t border-border pt-3 font-bold text-foreground"><span>Total due</span><span className="text-[#1a8a9d]">{new Intl.NumberFormat('vi-VN').format(booking.totalAmount + booking.depositAmount)} ₫</span></div></div></section>
      {!checkout ? <><div className="space-y-2"><h2 className="text-sm font-bold text-foreground">Payment method</h2>{methods.map((item) => <button key={item.id} onClick={() => setMethod(item.id)} className={`flex w-full items-center gap-3 rounded-xl border p-3 text-left transition ${method === item.id ? 'border-[#1a8a9d] bg-[#1a8a9d]/5 ring-1 ring-[#1a8a9d]/20' : 'border-border bg-card hover:bg-muted/40'}`}><span className={`flex size-10 items-center justify-center rounded-xl ${method === item.id ? 'bg-[#1a8a9d]/10 text-[#1a8a9d]' : 'bg-muted text-muted-foreground'}`}><CreditCard size={18} /></span><span className="min-w-0 flex-1"><span className="block text-sm font-bold text-foreground">{item.label}</span><span className="mt-0.5 block text-xs text-muted-foreground">{item.detail}</span></span><span className={`flex size-5 items-center justify-center rounded-full border ${method === item.id ? 'border-[#1a8a9d] bg-[#1a8a9d] text-white' : 'border-border'}`}>{method === item.id && <Check size={12} />}</span></button>)}</div>{error && <p role="alert" className="rounded-xl bg-destructive/10 p-3 text-xs text-destructive">{error}</p>}<button onClick={() => void beginCheckout()} disabled={isPending} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485] disabled:opacity-50"><LockKeyhole size={16} />{isPending ? 'Creating payment…' : 'Continue to payment'}<ArrowRight size={16} /></button><p className="flex items-center justify-center gap-1.5 text-[11px] text-muted-foreground"><ShieldCheck size={13} className="text-[#1a8a9d]" />Your payment details are protected.</p></> : <section className="space-y-4 rounded-2xl border border-[#1a8a9d]/25 bg-[#1a8a9d]/5 p-4"><div className="flex items-center gap-2 text-sm font-bold text-foreground"><QrCode size={18} className="text-[#1a8a9d]" /> Payment session ready</div>{checkout.qrCodeUrl && <img src={checkout.qrCodeUrl} alt="Payment QR code" className="mx-auto size-48 rounded-xl border border-border bg-white p-2" />}<p className="text-center text-xs leading-5 text-muted-foreground">Complete your payment on the gateway, then return here to check the transaction status.</p><a href={checkout.paymentUrl} target="_blank" rel="noreferrer" className="flex h-11 items-center justify-center rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485]">Continue to {checkout.method}</a><button onClick={() => navigate(`/payments/result?bookingId=${encodeURIComponent(bookingId)}&paymentId=${encodeURIComponent(checkout.paymentId)}`)} className="h-11 w-full rounded-xl border border-border bg-background text-sm font-semibold text-foreground hover:bg-muted">I've paid</button></section>}
    </main>
  )
}

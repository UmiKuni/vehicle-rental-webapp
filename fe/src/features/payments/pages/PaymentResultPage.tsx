import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, Clock3, XCircle } from 'lucide-react'
import { paymentApi, type BookingPaymentSummary } from '../api/payment.api'
import { ROUTES } from '@/shared/constants'

export default function PaymentResultPage() {
  const [params] = useSearchParams()
  const bookingId = params.get('bookingId') ?? ''
  const paymentId = params.get('paymentId')
  const [summary, setSummary] = useState<BookingPaymentSummary | null>(null)
  const [status, setStatus] = useState('')
  const [error, setError] = useState(false)
  const hasReference = Boolean(bookingId || paymentId)

  useEffect(() => {
    let active = true
    const load = async () => {
      try {
        const result = paymentId ? await paymentApi.getPayment(paymentId) : await paymentApi.getBookingPayment(bookingId)
        if (!active) return
        if ('status' in result) setStatus(result.status)
        else {
          setSummary(result)
          setStatus(result.mainPayment?.status ?? '')
        }
      } catch { if (active) setError(true) }
    }
    if (hasReference) void load()
    return () => { active = false }
  }, [bookingId, hasReference, paymentId])

  const normalized = status.toLowerCase()
  const success = ['success', 'succeeded', 'paid', 'completed'].includes(normalized)
  const failed = ['failed', 'cancelled', 'canceled', 'rejected'].includes(normalized)
  const hasError = error || !hasReference
  const Icon = hasError || failed ? XCircle : success ? CheckCircle2 : Clock3

  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={ROUTES.BOOKINGS} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Chuyến đi</Link>
      <section className="flex flex-col items-center rounded-2xl border border-border bg-card px-5 py-9 text-center">
        <div className={`flex size-16 items-center justify-center rounded-2xl ${error || failed ? 'bg-destructive/10 text-destructive' : success ? 'bg-emerald-500/10 text-emerald-600' : 'bg-amber-500/10 text-amber-600'}`}><Icon size={30} /></div>
        <p className="mt-5 text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Kết quả thanh toán</p>
        <h1 className="mt-2 text-xl font-bold text-foreground">{hasError ? 'Chưa lấy được trạng thái' : success ? 'Thanh toán thành công' : failed ? 'Thanh toán chưa thành công' : 'Đang chờ xác nhận'}</h1>
        <p className="mt-2 max-w-xs text-sm leading-6 text-muted-foreground">{hasError ? 'Không thể kết nối tới dịch vụ thanh toán. Bạn có thể kiểm tra lại trong chi tiết chuyến đi.' : success ? 'Giao dịch đã được ghi nhận. Trạng thái chuyến đi sẽ được cập nhật sau khi hệ thống xác nhận.' : failed ? 'Giao dịch chưa hoàn tất. Bạn có thể quay lại và thử thanh toán lại.' : 'Cổng thanh toán chưa gửi kết quả cuối cùng. Hãy kiểm tra lại sau ít phút.'}</p>
        {(summary?.mainPayment || status) && <div className="mt-6 w-full rounded-xl bg-muted/50 p-3 text-left text-xs"><div className="flex justify-between"><span className="text-muted-foreground">Mã chuyến</span><span className="font-mono font-semibold text-foreground">{bookingId}</span></div>{summary?.mainPayment && <div className="mt-2 flex justify-between"><span className="text-muted-foreground">Số tiền</span><span className="font-bold text-foreground">{new Intl.NumberFormat('vi-VN').format(summary.mainPayment.amount)} ₫</span></div>}</div>}
        <Link to={bookingId ? `/bookings/${bookingId}` : ROUTES.BOOKINGS} className="mt-6 flex h-11 w-full items-center justify-center rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485]">Xem chuyến đi</Link>
      </section>
    </main>
  )
}

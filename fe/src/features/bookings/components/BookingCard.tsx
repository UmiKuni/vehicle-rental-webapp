import { Calendar, MapPin, AlertCircle } from 'lucide-react'
import type { Booking } from '@/types/booking.types'
import { useCancelBooking } from '../hooks/useBookings'
import { formatDate } from '@/shared/utils'

interface BookingCardProps {
  booking: Booking
}

const STATUS_CONFIG = {
  CONFIRMED: { label: 'Đã xác nhận', class: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30' },
  ACTIVE:    { label: 'Đang thuê',    class: 'bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border-cyan-500/30' },
  COMPLETED: { label: 'Đã hoàn thành', class: 'bg-zinc-500/10 text-zinc-600 dark:text-zinc-400 border-zinc-500/30' },
  CANCELLED: { label: 'Đã hủy',       class: 'bg-red-500/10 text-red-600 dark:text-red-400 border-red-500/30' },
}

export function BookingCard({ booking }: BookingCardProps) {
  const { mutate: cancelBooking, isPending } = useCancelBooking()
  const statusInfo = STATUS_CONFIG[booking.status] ?? STATUS_CONFIG.CONFIRMED

  const handleCancel = () => {
    if (window.confirm(`Bạn có chắc chắn muốn hủy đơn đặt xe ${booking.bookingCode}?`)) {
      cancelBooking(booking.id)
    }
  }

  return (
    <div className="overflow-hidden rounded-2xl border border-border bg-card p-4 space-y-3.5 shadow-xs">
      {/* Top Row: Code & Status */}
      <div className="flex items-center justify-between">
        <span className="font-mono text-xs font-bold text-foreground">
          {booking.bookingCode}
        </span>
        <span className={`rounded-full border px-2.5 py-0.5 text-[10px] font-bold ${statusInfo.class}`}>
          {statusInfo.label}
        </span>
      </div>

      {/* Car Info */}
      <div className="flex items-center gap-3">
        <img
          src={booking.vehicle.imageUrl}
          alt={booking.vehicle.name}
          className="h-16 w-24 rounded-xl object-cover border border-border/80"
        />
        <div className="min-w-0 flex-1">
          <h4 className="font-bold text-foreground text-sm truncate">
            {booking.vehicle.name}
          </h4>
          <p className="text-[11px] text-muted-foreground flex items-center gap-1 mt-0.5 truncate">
            <MapPin className="size-3 text-[#1a8a9d] shrink-0" />
            <span>{booking.vehicle.location}</span>
          </p>
          <p className="text-xs font-bold text-[#1a8a9d] mt-1">
            {new Intl.NumberFormat('vi-VN').format(booking.totalAmount)}đ
            <span className="text-[10px] text-muted-foreground font-normal"> ({booking.totalDays} ngày)</span>
          </p>
        </div>
      </div>

      {/* Date & Delivery Info */}
      <div className="rounded-xl bg-muted/40 p-2.5 space-y-1 text-[11px]">
        <div className="flex items-center gap-1.5 text-muted-foreground">
          <Calendar className="size-3.5 text-foreground/70 shrink-0" />
          <span className="font-medium text-foreground">
            {formatDate(booking.startDate)} → {formatDate(booking.endDate)}
          </span>
        </div>
        <div className="flex items-center justify-between text-muted-foreground pt-1 border-t border-border/50">
          <span>Hình thức:</span>
          <span className="font-semibold text-foreground">
            {booking.deliveryType === 'STATION' ? 'Tại trạm xe' : 'Giao tận nơi'}
          </span>
        </div>
      </div>

      {/* Actions */}
      {booking.status === 'CONFIRMED' && (
        <div className="flex items-center justify-end gap-2 pt-1 border-t border-border/60">
          <button
            type="button"
            disabled={isPending}
            onClick={handleCancel}
            className="rounded-lg px-3 py-1.5 text-xs font-semibold text-red-600 hover:bg-red-500/10 transition-colors"
          >
            Hủy đơn
          </button>
          <a
            href="tel:19001234"
            className="rounded-lg border border-border px-3 py-1.5 text-xs font-semibold text-foreground hover:bg-muted transition-colors flex items-center gap-1"
          >
            <AlertCircle className="size-3" />
            Trợ giúp
          </a>
        </div>
      )}
    </div>
  )
}

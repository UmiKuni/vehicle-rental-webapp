import { useState } from 'react'
import { Link } from 'react-router-dom'
import { CalendarCheck, Car, Sparkles, Plus } from 'lucide-react'
import { useBookings } from '../hooks/useBookings'
import { BookingCard } from '../components/BookingCard'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ROUTES } from '@/shared/constants'

type TabType = 'UPCOMING' | 'HISTORY'

export default function MyBookingsPage() {
  const [activeTab, setActiveTab] = useState<TabType>('UPCOMING')
  const { data: bookings = [], isLoading } = useBookings()

  const upcomingBookings = bookings.filter((b) => b.status === 'CONFIRMED' || b.status === 'ACTIVE')
  const historyBookings = bookings.filter((b) => b.status === 'COMPLETED' || b.status === 'CANCELLED')

  const currentList = activeTab === 'UPCOMING' ? upcomingBookings : historyBookings

  return (
    <div className="space-y-4 pb-24 px-4 pt-4">
      {/* Top Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-foreground flex items-center gap-2">
            <CalendarCheck className="size-5 text-[#1a8a9d]" />
            Chuyến đi của tôi
          </h1>
          <p className="text-xs text-muted-foreground mt-0.5">
            Quản lý lịch trình đặt xe và lịch sử di chuyển
          </p>
        </div>

        <Link
          to={ROUTES.SEARCH}
          className="flex h-9 items-center gap-1.5 rounded-xl bg-[#1a8a9d] px-3 text-xs font-bold text-white shadow-xs hover:bg-[#147485] active-press transition-colors"
        >
          <Plus className="size-3.5" />
          Đặt xe mới
        </Link>
      </div>

      {/* Segmented Tab */}
      <div className="grid grid-cols-2 p-1 bg-muted/60 rounded-2xl">
        <button
          type="button"
          onClick={() => setActiveTab('UPCOMING')}
          className={`py-2 text-center text-xs font-bold rounded-xl transition-all ${
            activeTab === 'UPCOMING'
              ? 'bg-background text-foreground shadow-xs'
              : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          Sắp tới & Đang đi ({upcomingBookings.length})
        </button>
        <button
          type="button"
          onClick={() => setActiveTab('HISTORY')}
          className={`py-2 text-center text-xs font-bold rounded-xl transition-all ${
            activeTab === 'HISTORY'
              ? 'bg-background text-foreground shadow-xs'
              : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          Lịch sử ({historyBookings.length})
        </button>
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="space-y-3">
          <CardSkeleton />
          <CardSkeleton />
        </div>
      )}

      {/* Empty State */}
      {!isLoading && currentList.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center px-4 rounded-3xl border border-dashed border-border bg-card/50">
          <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-muted text-muted-foreground mb-3">
            <Car className="size-8" />
          </div>
          <h3 className="font-bold text-foreground text-sm">
            {activeTab === 'UPCOMING' ? 'Chưa có chuyến đi nào sắp tới' : 'Chưa có lịch sử chuyến đi'}
          </h3>
          <p className="mt-1 text-xs text-muted-foreground max-w-xs leading-relaxed">
            {activeTab === 'UPCOMING'
              ? 'Khám phá hơn 60+ dòng xe đời mới sẵn sàng phục vụ cho hành trình tiếp theo của bạn.'
              : 'Các chuyến đi đã hoàn thành hoặc đã hủy sẽ được lưu trữ tại đây.'}
          </p>
          <Link
            to={ROUTES.SEARCH}
            className="mt-5 inline-flex items-center gap-1.5 rounded-xl bg-[#1a8a9d] px-5 py-2.5 text-xs font-bold text-white shadow-xs hover:bg-[#147485] active-press transition-colors"
          >
            <Sparkles className="size-3.5" />
            Tìm xe tự lái ngay
          </Link>
        </div>
      )}

      {/* Bookings List */}
      {!isLoading && currentList.length > 0 && (
        <div className="space-y-3">
          {currentList.map((booking) => (
            <BookingCard key={booking.id} booking={booking} />
          ))}
        </div>
      )}
    </div>
  )
}

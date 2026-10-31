import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, CalendarCheck } from 'lucide-react'
import { useBookings } from '../hooks/useBookings'
import { BookingCard } from '../components/BookingCard'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ErrorState } from '@/shared/components/common/StateViews'
import { ROUTES } from '@/shared/constants'

export default function BookingDetailPage() {
  const { bookingId } = useParams()
  const { data: bookings = [], isLoading, isFetching, isError, refetch } = useBookings()
  const booking = bookings.find((item) => item.id === bookingId)

  return (
    <main className="space-y-4 px-4 pt-5 pb-24">
      <Link to={ROUTES.BOOKINGS} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground">
        <ArrowLeft size={15} /> My trips
      </Link>
      <h1 className="flex items-center gap-2 text-xl font-bold text-foreground">
        <CalendarCheck className="size-5 text-[#1a8a9d]" /> Booking details
      </h1>
      {isLoading && <CardSkeleton />}
      {isError && <ErrorState message="Booking details could not be loaded." onRetry={() => void refetch()} />}
      {!isLoading && !isError && booking && <BookingCard booking={booking} />}
      {!isLoading && !isFetching && !isError && !booking && (
        <ErrorState message="This booking could not be found." onRetry={() => void refetch()} />
      )}
    </main>
  )
}

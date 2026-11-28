import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, CalendarCheck } from 'lucide-react'
import { useBooking } from '../hooks/useBookings'
import { BookingCard } from '../components/BookingCard'
import { RentalJourney } from '../components/RentalJourney'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ErrorState } from '@/shared/components/common/StateViews'
import { ROUTES } from '@/shared/constants'
import { reviewApi } from '@/features/vehicles/api/review.api'
import { getErrorMessage } from '@/shared/lib/query-client'
import { toast } from 'sonner'

export default function BookingDetailPage() {
  const { bookingId } = useParams()
  const queryClient = useQueryClient()
  const [rating, setRating] = useState(5)
  const [review, setReview] = useState('')
  const [reviewSubmitted, setReviewSubmitted] = useState(false)
  const { data: booking, isLoading, isError, refetch } = useBooking(bookingId)
  const submitReview = useMutation({
    mutationFn: () => reviewApi.createForBooking(bookingId!, rating, review),
    onSuccess: () => {
      toast.success('Review submitted.')
      setReviewSubmitted(true)
      setReview('')
      void queryClient.invalidateQueries({ queryKey: ['vehicle-reviews', booking?.vehicleId] })
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

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
      {!isLoading && !isError && booking && <>
        <RentalJourney booking={booking} />
        <BookingCard booking={booking} />
        {booking.status === 'COMPLETED' && !reviewSubmitted && <form onSubmit={(event) => { event.preventDefault(); if (!review.trim() || submitReview.isPending) return; submitReview.mutate() }} className="space-y-3 rounded-2xl border border-border bg-card p-4">
          <div><h2 className="text-sm font-bold text-foreground">Review this trip</h2><p className="mt-1 text-xs text-muted-foreground">Your feedback helps other renters make a decision.</p></div>
          <label className="block space-y-1"><span className="text-xs font-semibold text-foreground">Rating</span><select value={rating} onChange={(event) => setRating(Number(event.target.value))} className="h-10 w-full rounded-xl border border-border bg-background px-3 text-sm"><option value={5}>5 — Excellent</option><option value={4}>4 — Good</option><option value={3}>3 — Average</option><option value={2}>2 — Poor</option><option value={1}>1 — Very poor</option></select></label>
          <label className="block space-y-1"><span className="text-xs font-semibold text-foreground">Comment</span><textarea value={review} onChange={(event) => setReview(event.target.value)} required maxLength={2000} rows={3} className="w-full rounded-xl border border-border bg-background px-3 py-2 text-sm" placeholder="Share your experience" /></label>
          <button type="submit" disabled={submitReview.isPending || !review.trim()} className="h-10 w-full rounded-xl bg-[#1a8a9d] text-sm font-bold text-white disabled:opacity-50">{submitReview.isPending ? 'Submitting…' : 'Submit review'}</button>
        </form>}
        {booking.status === 'COMPLETED' && reviewSubmitted && <p className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3 text-sm text-emerald-700">Thanks for sharing your review.</p>}
        {(booking.status === 'PAID' || booking.status === 'ACTIVE' || booking.status === 'IN_PROGRESS' || booking.status === 'COMPLETED') && (
          <div className="grid grid-cols-2 gap-2">
            {booking.status === 'PAID' && <Link to={`/bookings/${booking.id}/pickup`} className="flex h-11 items-center justify-center rounded-xl border border-border bg-card text-xs font-semibold text-foreground">Pickup inspection</Link>}
            {(booking.status === 'ACTIVE' || booking.status === 'IN_PROGRESS') && <Link to={`/bookings/${booking.id}/return`} className="flex h-11 items-center justify-center rounded-xl border border-border bg-card text-xs font-semibold text-foreground">Return inspection</Link>}
            {booking.status === 'COMPLETED' && <Link to={`/bookings/${booking.id}/settlement`} className="flex h-11 items-center justify-center rounded-xl border border-border bg-card text-xs font-semibold text-foreground">Settlement &amp; deposit refund</Link>}
          </div>
        )}
      </>}
    </main>
  )
}

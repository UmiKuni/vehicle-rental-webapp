import { Check, Circle, Clock3 } from 'lucide-react'
import type { Booking, BookingStatus } from '@/types/booking.types'
import { formatDate } from '@/shared/utils'

const STEP_LABELS = ['Request', 'Confirm and pay', 'Pick up', 'Return']

const ACTIVE_STEP: Record<BookingStatus, number> = {
  PENDING: 0,
  CONFIRMED: 1,
  PAID: 2,
  ACTIVE: 3,
  IN_PROGRESS: 3,
  COMPLETED: 4,
  CANCELLED: -1,
  REJECTED: -1,
  EXPIRED: -1,
}

const STATUS_COPY: Record<BookingStatus, { title: string; detail: (booking: Booking) => string }> = {
  PENDING: {
    title: 'Waiting for the owner',
    detail: () => 'Your request was sent. The owner must confirm it before you can pay.',
  },
  CONFIRMED: {
    title: 'Payment is your next step',
    detail: () => 'The owner confirmed your request. Pay securely to hold the vehicle for your trip.',
  },
  PAID: {
    title: 'Prepare for pickup',
    detail: (booking) => `Payment is complete. Bring your licence and identification on ${formatDate(booking.startDate)}.`,
  },
  ACTIVE: {
    title: 'Your rental is active',
    detail: (booking) => `Enjoy your trip and return the vehicle by ${formatDate(booking.endDate)}.`,
  },
  IN_PROGRESS: {
    title: 'Your rental is active',
    detail: (booking) => `Enjoy your trip and return the vehicle by ${formatDate(booking.endDate)}.`,
  },
  COMPLETED: {
    title: 'Trip completed',
    detail: () => 'The vehicle was returned. Review the settlement and deposit details, then share your experience.',
  },
  CANCELLED: {
    title: 'Booking cancelled',
    detail: () => 'No further trip action is required. Any refund or fee follows the cancellation policy shown in your settlement.',
  },
  REJECTED: {
    title: 'Owner could not accept this request',
    detail: () => 'This booking will not continue. Return to search to choose another available vehicle.',
  },
  EXPIRED: {
    title: 'Booking expired',
    detail: () => 'The required action was not completed in time. Start a new request to check current availability and pricing.',
  },
}

export function RentalJourney({ booking }: { booking: Booking }) {
  const activeStep = ACTIVE_STEP[booking.status]
  const copy = STATUS_COPY[booking.status]
  const isTerminalException = activeStep < 0

  return (
    <section aria-labelledby="rental-journey-title" className="rounded-2xl border border-border bg-card p-4">
      <h2 id="rental-journey-title" className="text-base font-bold tracking-tight text-foreground">What happens next</h2>
      <div className="mt-3 rounded-xl bg-[#1a8a9d]/8 p-3">
        <p className="text-sm font-bold text-foreground">{copy.title}</p>
        <p className="mt-1 text-sm leading-6 text-muted-foreground">{copy.detail(booking)}</p>
      </div>
      {!isTerminalException && (
        <ol className="mt-4 grid grid-cols-4 gap-1" aria-label="Rental progress">
          {STEP_LABELS.map((label, index) => {
            const completed = index < activeStep
            const current = index === activeStep || (activeStep === 4 && index === 3)
            return (
              <li key={label} aria-current={current ? 'step' : undefined} className="min-w-0 text-center">
                <span className={`mx-auto flex size-8 items-center justify-center rounded-full border ${completed || activeStep === 4 ? 'border-[#1a8a9d] bg-[#1a8a9d] text-white' : current ? 'border-[#1a8a9d] bg-[#1a8a9d]/10 text-[#1a8a9d]' : 'border-border text-muted-foreground'}`}>
                  {completed || activeStep === 4 ? <Check size={15} /> : current ? <Clock3 size={15} /> : <Circle size={12} />}
                </span>
                <span className={`mt-1.5 block text-[11px] leading-4 ${current ? 'font-bold text-foreground' : 'font-medium text-muted-foreground'}`}>{label}</span>
              </li>
            )
          })}
        </ol>
      )}
    </section>
  )
}

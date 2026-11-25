import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import apiClient from '@/shared/lib/axios'
import { ShieldAlert, LogIn } from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import type { Vehicle } from '@/types/vehicle.types'
import type { Booking } from '@/types/booking.types'
import { useCreateBooking } from '@/features/bookings/hooks/useBookings'
import { useAuth } from '@/shared/contexts/AuthContext'
import { availabilityApi, pricingApi } from '@/features/vehicles/api/vehicle.api'
import { ROUTES } from '@/shared/constants'
import { Button } from '@/shared/components/ui/Button'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/shared/components/ui/Dialog'
import { BookingPriceSummary } from './BookingPriceSummary'
import { BookingDatesDelivery } from './BookingDatesDelivery'

function toDateInputValue(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function addDaysToDateInput(value: string, days: number) {
  const date = new Date(`${value}T12:00:00`)
  date.setDate(date.getDate() + days)
  return toDateInputValue(date)
}

interface BookingModalProps {
  vehicle: Vehicle | null
  onClose: () => void
  onSuccess: (booking: Booking) => void
  initialStartDate?: string
  initialEndDate?: string
}

export function BookingModal({ vehicle, onClose, onSuccess, initialStartDate, initialEndDate }: BookingModalProps) {
  const { user } = useAuth()
  const isOwnerAccount = user?.role === 'OWNER'
  const navigate = useNavigate()
  const location = useLocation()
  const { mutate: createBooking, isPending } = useCreateBooking()

  const tomorrowDate = new Date()
  tomorrowDate.setDate(tomorrowDate.getDate() + 1)
  const tomorrow = toDateInputValue(tomorrowDate)
  const requestedStart = initialStartDate?.slice(0, 10)
  const initialStart = requestedStart && requestedStart >= tomorrow ? requestedStart : tomorrow
  const requestedEnd = initialEndDate?.slice(0, 10)

  const [startDate, setStartDate] = useState(initialStart)
  const [endDate, setEndDate] = useState(requestedEnd && requestedEnd > initialStart ? requestedEnd : addDaysToDateInput(initialStart, 2))
  const [pickupLocation, setPickupLocation] = useState('Vehicle rental station')
  const validDates = startDate >= tomorrow && endDate > startDate
  const availability = useQuery({
    queryKey: ['vehicle-availability', vehicle?.id, startDate, endDate],
    queryFn: () => availabilityApi.check(vehicle!.id, startDate, endDate),
    enabled: !!vehicle && validDates && !isOwnerAccount,
    staleTime: 30_000,
  })
  const quote = useQuery({
    queryKey: ['vehicle-pricing-quote', vehicle?.id, startDate, endDate],
    queryFn: () => pricingApi.quote(vehicle!.id, startDate, endDate),
    enabled: !!vehicle && validDates,
    staleTime: 30_000,
  })
  const eligibility = useQuery({
    queryKey: ['booking-eligibility', user?.id],
    queryFn: async () => (await apiClient.get<{ isEligible: boolean; ineligibilityReason?: string | null }>(`/users/eligibility/${user!.id}`)).data,
    enabled: !!user && !!vehicle && !isOwnerAccount,
    staleTime: 60_000,
  })
  const contact = useQuery({
    queryKey: ['user-contact', user?.id],
    queryFn: async () => (await apiClient.get<{ fullName: string; phone: string }>(`/users/contact/${user!.id}`)).data,
    enabled: !!user && !!vehicle && !isOwnerAccount,
    staleTime: 5 * 60 * 1000,
  })

  if (!vehicle) return null

  const eligibilityReason = eligibility.data?.ineligibilityReason ?? ''
  const needsVerification = /CCCD|GPLX|license/i.test(eligibilityReason)
  const start = new Date(startDate)
  const end = new Date(endDate)
  const diffTime = Math.max(0, end.getTime() - start.getTime())
  const totalDays = Math.max(1, Math.ceil(diffTime / (1000 * 60 * 60 * 24)))
  const rentalAmount = quote.data?.total.amount ?? totalDays * vehicle.pricePerDay
  const depositAmount = quote.data?.depositAmount.amount ?? vehicle.depositAmount
  const totalAmount = rentalAmount + depositAmount

  const requestLogin = () => {
    const previousState = location.state && typeof location.state === 'object' ? location.state : {}
    navigate(ROUTES.PROFILE_AUTH, {
      state: {
        from: {
          pathname: location.pathname,
          search: location.search,
          state: { ...previousState, pickupAt: startDate, returnAt: endDate, resumeBookingVehicleId: vehicle.id, resumeBookingVehicle: vehicle },
        },
      },
      replace: true,
    })
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!user) {
      requestLogin()
      return
    }
    if (isOwnerAccount) return
    if (!validDates || !pickupLocation.trim() || eligibility.isLoading || eligibility.isError || !eligibility.data?.isEligible || availability.isLoading || availability.data?.isAvailable === false || quote.isLoading || quote.isError) return

    createBooking(
      {
        vehicleId: vehicle.id,
        startDate,
        endDate,
        pickupLocation: pickupLocation.trim(),
      },
      {
        onSuccess: (booking) => {
          onSuccess(booking)
          onClose()
        },
      }
    )
  }

  return (
    <Dialog open onOpenChange={(open) => { if (!open) onClose() }}>
      <DialogContent className="booking-modal-panel bottom-0 left-0 top-auto flex max-h-[88dvh] w-full max-w-none translate-x-0 translate-y-0 flex-col overflow-hidden rounded-b-none rounded-t-2xl border-border bg-background p-0 shadow-2xl sm:bottom-auto sm:left-1/2 sm:top-1/2 sm:max-w-lg sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-2xl">
        {/* Sticky Header */}
        <div className="flex items-center justify-between border-b border-border bg-background px-5 pb-3 pt-4 pr-14 shrink-0">
          <div className="flex items-center gap-3 min-w-0">
            <img
              src={vehicle.imageUrl}
              alt={vehicle.name}
              className="h-11 w-16 rounded-xl object-cover border border-border shrink-0"
            />
            <div className="min-w-0">
              <DialogTitle className="truncate text-sm font-bold leading-5 text-foreground sm:text-base">Book {vehicle.name}</DialogTitle>
              <DialogDescription className="sr-only">Choose rental dates, pickup location, and review the complete price before booking.</DialogDescription>
              <p className="text-xs font-bold text-[#1a8a9d]">
                {new Intl.NumberFormat('en-US').format(vehicle.pricePerDay)} VND / day
              </p>
            </div>
          </div>
        </div>

        {/* Scrollable Form Body */}
        <form id="booking-form" onSubmit={handleSubmit} className="booking-modal-form overflow-y-auto flex-1 px-5 py-4 space-y-4 text-xs">
          {/* Guest notice */}
          {!user && (
            <div className="rounded-2xl border border-cyan-500/30 bg-cyan-500/10 p-3 text-xs flex items-start gap-2.5">
              <LogIn className="size-4 text-[#1a8a9d] shrink-0 mt-0.5" />
              <div className="flex-1 min-w-0">
                <span className="font-bold text-foreground">You are browsing as a Guest</span>
                <p className="text-[11px] text-muted-foreground mt-0.5 leading-relaxed">
                  Sign in to reserve your car and unlock member-exclusive deals.
                </p>
              </div>
            </div>
          )}

          {user && eligibility.isPending && <p className="text-xs text-muted-foreground">Checking booking eligibility…</p>}
          {isOwnerAccount && <div className="rounded-xl border border-[#b8d8cb] bg-[#edf7f2] p-3 text-xs leading-5 text-[#315c4e]">This account uses the owner workspace. Customer booking is kept in the customer account flow. <Link to={ROUTES.OWNER_DASHBOARD} className="font-bold underline">Open owner workspace</Link></div>}
          {user && eligibility.isError && <div role="alert" className="rounded-xl bg-destructive/10 p-3 text-xs text-destructive">Could not check booking eligibility. <button type="button" className="underline" onClick={() => void eligibility.refetch()}>Retry</button></div>}
          {user && eligibility.data && !eligibility.data.isEligible && (
            <div className="rounded-2xl border border-amber-300/60 bg-amber-500/10 p-3 text-xs flex items-start gap-2.5">
              <ShieldAlert className="size-4 text-amber-500 shrink-0 mt-0.5" />
              <div className="flex-1 min-w-0">
                <span className="font-bold text-amber-700 dark:text-amber-400">Booking requirements not met</span>
                <p className="text-[11px] text-muted-foreground mt-0.5 leading-relaxed">
                  {eligibilityReason || 'This account is not currently eligible to book.'}
                </p>
                <Link to={needsVerification ? ROUTES.PROFILE_VERIFY : ROUTES.PROFILE_EDIT} className="mt-1 inline-block text-[11px] font-bold text-[#1a8a9d] hover:underline">
                  Resolve account requirement →
                </Link>
              </div>
            </div>
          )}

          {/* Dates & Delivery */}
          <BookingDatesDelivery
            startDate={startDate}
            endDate={endDate}
            tomorrow={tomorrow}
            pickupLocation={pickupLocation}
            onStartDateChange={setStartDate}
            onEndDateChange={setEndDate}
            onPickupLocationChange={setPickupLocation}
          />

          {/* Booking contact is managed by the user account API. */}
          {user && (
            <div className="rounded-xl border border-border bg-muted/30 px-3 py-2.5 text-[11px] text-muted-foreground">
              Booking contact: <strong className="text-foreground">{contact.data?.fullName || user.fullName}</strong>
              {contact.isLoading ? ' · Loading phone…' : contact.data?.phone ? ` · ${contact.data.phone}` : ''}
              {contact.isError && <span className="ml-1 text-destructive">· Could not load account contact.</span>}
            </div>
          )}

          {/* Pricing Breakdown */}
          <BookingPriceSummary
            totalDays={totalDays}
            rentalAmount={rentalAmount}
            depositAmount={depositAmount}
            totalAmount={totalAmount}
          />
          {validDates && quote.isLoading && <p className="text-xs text-muted-foreground">Calculating rental quote…</p>}
          {quote.isError && <p role="alert" className="text-xs font-medium text-destructive">Could not calculate the rental quote. <button type="button" className="underline" onClick={() => void quote.refetch()}>Try again</button></p>}
          {!validDates && <p role="alert" className="text-xs font-medium text-destructive">Return date must be after pickup date.</p>}
          {validDates && availability.isLoading && <p className="text-xs text-muted-foreground">Checking vehicle availability…</p>}
          {availability.data?.isAvailable === false && <p role="alert" className="text-xs font-medium text-destructive">{availability.data.conflictReason || 'This vehicle is not available for the selected dates.'}</p>}
          {availability.isError && <p role="alert" className="text-xs font-medium text-destructive">Could not check availability. <button type="button" className="underline" onClick={() => void availability.refetch()}>Try again</button></p>}
        </form>

        {/* Sticky Action Footer */}
        <div className="px-5 py-3 border-t border-border bg-background/95 backdrop-blur-sm shrink-0 safe-area-pb">
          {user ? (
            <Button
              form="booking-form"
              type="submit"
              loading={isPending}
              disabled={isOwnerAccount || !validDates || !pickupLocation.trim() || eligibility.isLoading || eligibility.isError || !eligibility.data?.isEligible || availability.isLoading || availability.isError || availability.data?.isAvailable === false || quote.isLoading || quote.isError}
              className="w-full h-12 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-bold text-sm shadow-md transition-all active-press"
            >
              Confirm Booking · {new Intl.NumberFormat('en-US').format(totalAmount)} VND
            </Button>
          ) : (
            <Button
              type="button"
              onClick={requestLogin}
              className="w-full h-12 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-bold text-sm shadow-md transition-all active-press flex items-center justify-center gap-2"
            >
              <LogIn className="size-4" />
              <span>Sign in to book · {new Intl.NumberFormat('en-US').format(totalAmount)} VND</span>
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}

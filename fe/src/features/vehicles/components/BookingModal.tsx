import { useState } from 'react'
import { X, ShieldAlert, LogIn } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import type { Vehicle } from '@/types/vehicle.types'
import type { DeliveryType } from '@/types/booking.types'
import { useCreateBooking } from '@/features/bookings/hooks/useBookings'
import { useAuth } from '@/shared/contexts/AuthContext'
import { useKycStatus } from '@/features/profile/hooks/useProfile'
import { ROUTES } from '@/shared/constants'
import { Button } from '@/shared/components/ui/Button'
import { BookingPriceSummary } from './BookingPriceSummary'
import { BookingDatesDelivery } from './BookingDatesDelivery'

interface BookingModalProps {
  vehicle: Vehicle | null
  onClose: () => void
  onSuccess: () => void
}

export function BookingModal({ vehicle, onClose, onSuccess }: BookingModalProps) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const { data: kyc } = useKycStatus(!!user)
  const { mutate: createBooking, isPending } = useCreateBooking()

  const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0]
  const dayAfter = new Date(Date.now() + 86400000 * 3).toISOString().split('T')[0]

  const [startDate, setStartDate] = useState(tomorrow)
  const [endDate, setEndDate] = useState(dayAfter)
  const [deliveryType, setDeliveryType] = useState<DeliveryType>('STATION')
  const [deliveryAddress, setDeliveryAddress] = useState('')
  const [customerName, setCustomerName] = useState(user?.fullName ?? '')
  const [customerPhone, setCustomerPhone] = useState('0912345678')

  if (!vehicle) return null

  const isUnverified = user && (!kyc || kyc.status === 'UNVERIFIED')
  const start = new Date(startDate)
  const end = new Date(endDate)
  const diffTime = Math.max(0, end.getTime() - start.getTime())
  const totalDays = Math.max(1, Math.ceil(diffTime / (1000 * 60 * 60 * 24)))
  const deliveryFee = deliveryType === 'DOORSTEP' ? 100000 : 0
  const rentalAmount = totalDays * vehicle.pricePerDay
  const totalAmount = rentalAmount + deliveryFee

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!user) {
      navigate(ROUTES.LOGIN, { state: { from: ROUTES.SEARCH } })
      return
    }

    createBooking(
      {
        vehicleId: vehicle.id,
        startDate,
        endDate,
        deliveryType,
        deliveryAddress: deliveryType === 'DOORSTEP' ? deliveryAddress : undefined,
        customerName: customerName.trim() || user.fullName,
        customerPhone: customerPhone.trim(),
      },
      {
        onSuccess: () => {
          onSuccess()
          onClose()
        },
      }
    )
  }

  return (
    <div className="fixed inset-0 z-[100] flex items-end sm:items-center justify-center bg-black/70 backdrop-blur-xs p-0 sm:p-4">
      <div className="absolute inset-0" onClick={onClose} aria-hidden="true" />

      <div className="relative w-full max-w-lg max-h-[85vh] flex flex-col rounded-t-[28px] sm:rounded-3xl bg-background border border-border shadow-2xl overflow-hidden animate-in fade-in-0 slide-in-from-bottom-6">
        {/* Sticky Header */}
        <div className="flex items-center justify-between px-5 pt-4 pb-3 border-b border-border bg-background/95 backdrop-blur-sm shrink-0">
          <div className="flex items-center gap-3 min-w-0">
            <img
              src={vehicle.imageUrl}
              alt={vehicle.name}
              className="h-11 w-16 rounded-xl object-cover border border-border shrink-0"
            />
            <div className="min-w-0">
              <h2 className="text-sm sm:text-base font-bold text-foreground truncate">{vehicle.name}</h2>
              <p className="text-xs font-bold text-[#1a8a9d]">
                {new Intl.NumberFormat('en-US').format(vehicle.pricePerDay)} VND / day
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-full p-2 text-muted-foreground hover:bg-muted active-press min-h-[44px] min-w-[44px] flex items-center justify-center"
          >
            <X className="size-5" />
          </button>
        </div>

        {/* Scrollable Form Body */}
        <form id="booking-form" onSubmit={handleSubmit} className="overflow-y-auto flex-1 px-5 py-4 space-y-4 text-xs">
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

          {/* KYC notice if user logged in but unverified */}
          {isUnverified && (
            <div className="rounded-2xl border border-amber-300/60 bg-amber-500/10 p-3 text-xs flex items-start gap-2.5">
              <ShieldAlert className="size-4 text-amber-500 shrink-0 mt-0.5" />
              <div className="flex-1 min-w-0">
                <span className="font-bold text-amber-700 dark:text-amber-400">Account has not completed KYC</span>
                <p className="text-[11px] text-muted-foreground mt-0.5 leading-relaxed">
                  You can still reserve now. Please verify your Passport or National ID before vehicle pickup!
                </p>
                <Link to={ROUTES.PROFILE_VERIFY} className="mt-1 inline-block text-[11px] font-bold text-[#1a8a9d] hover:underline">
                  Verify now →
                </Link>
              </div>
            </div>
          )}

          {/* Dates & Delivery */}
          <BookingDatesDelivery
            startDate={startDate}
            endDate={endDate}
            tomorrow={tomorrow}
            deliveryType={deliveryType}
            deliveryAddress={deliveryAddress}
            isLoggedIn={!!user}
            onStartDateChange={setStartDate}
            onEndDateChange={setEndDate}
            onDeliveryTypeChange={setDeliveryType}
            onDeliveryAddressChange={setDeliveryAddress}
          />

          {/* Contact Details (For logged-in users) */}
          {user && (
            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="block text-[11px] font-semibold text-muted-foreground mb-1">Full Name</label>
                <input
                  type="text"
                  value={customerName}
                  onChange={(e) => setCustomerName(e.target.value)}
                  required
                  className="w-full h-11 rounded-xl border border-border bg-muted/40 px-3 text-xs text-foreground"
                />
              </div>
              <div>
                <label className="block text-[11px] font-semibold text-muted-foreground mb-1">Phone Number</label>
                <input
                  type="tel"
                  value={customerPhone}
                  onChange={(e) => setCustomerPhone(e.target.value)}
                  required
                  className="w-full h-11 rounded-xl border border-border bg-muted/40 px-3 text-xs text-foreground"
                />
              </div>
            </div>
          )}

          {/* Pricing Breakdown */}
          <BookingPriceSummary
            totalDays={totalDays}
            rentalAmount={rentalAmount}
            deliveryFee={deliveryFee}
            depositAmount={vehicle.depositAmount}
            totalAmount={totalAmount}
          />
        </form>

        {/* Sticky Action Footer */}
        <div className="px-5 py-3 border-t border-border bg-background/95 backdrop-blur-sm shrink-0 safe-area-pb">
          {user ? (
            <Button
              form="booking-form"
              type="submit"
              loading={isPending}
              className="w-full h-12 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-bold text-sm shadow-md transition-all active-press"
            >
              Confirm Booking · {new Intl.NumberFormat('en-US').format(totalAmount)} VND
            </Button>
          ) : (
            <Button
              type="button"
              onClick={() => navigate(ROUTES.LOGIN, { state: { from: ROUTES.SEARCH } })}
              className="w-full h-12 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-bold text-sm shadow-md transition-all active-press flex items-center justify-center gap-2"
            >
              <LogIn className="size-4" />
              <span>Sign in to book · {new Intl.NumberFormat('en-US').format(totalAmount)} VND</span>
            </Button>
          )}
        </div>
      </div>
    </div>
  )
}

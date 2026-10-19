import { Calendar, MapPin } from 'lucide-react'
import type { DeliveryType } from '@/types/booking.types'

interface BookingDatesDeliveryProps {
  startDate: string
  endDate: string
  tomorrow: string
  deliveryType: DeliveryType
  deliveryAddress: string
  isLoggedIn: boolean
  onStartDateChange: (val: string) => void
  onEndDateChange: (val: string) => void
  onDeliveryTypeChange: (val: DeliveryType) => void
  onDeliveryAddressChange: (val: string) => void
}

export function BookingDatesDelivery({
  startDate,
  endDate,
  tomorrow,
  deliveryType,
  deliveryAddress,
  isLoggedIn,
  onStartDateChange,
  onEndDateChange,
  onDeliveryTypeChange,
  onDeliveryAddressChange,
}: BookingDatesDeliveryProps) {
  return (
    <>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-[11px] font-semibold text-muted-foreground mb-1">Pick-up Date</label>
          <div className="relative">
            <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
            <input
              type="date"
              value={startDate}
              min={tomorrow}
              onChange={(e) => onStartDateChange(e.target.value)}
              className="w-full h-11 rounded-xl border border-border bg-muted/40 pl-9 pr-2 text-xs font-medium text-foreground focus:outline-none focus:border-[#1a8a9d]"
            />
          </div>
        </div>
        <div>
          <label className="block text-[11px] font-semibold text-muted-foreground mb-1">Return Date</label>
          <div className="relative">
            <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
            <input
              type="date"
              value={endDate}
              min={startDate}
              onChange={(e) => onEndDateChange(e.target.value)}
              className="w-full h-11 rounded-xl border border-border bg-muted/40 pl-9 pr-2 text-xs font-medium text-foreground focus:outline-none focus:border-[#1a8a9d]"
            />
          </div>
        </div>
      </div>

      <div>
        <label className="block text-[11px] font-semibold text-muted-foreground mb-1.5">Delivery Option</label>
        <div className="grid grid-cols-2 gap-2">
          <button
            type="button"
            onClick={() => onDeliveryTypeChange('STATION')}
            className={`p-3 rounded-xl border text-left transition-all min-h-[44px] ${
              deliveryType === 'STATION'
                ? 'border-[#1a8a9d] bg-[#1a8a9d]/10 text-foreground font-bold'
                : 'border-border bg-muted/30 text-muted-foreground hover:bg-muted/50'
            }`}
          >
            <div className="flex items-center gap-1.5 text-xs">
              <MapPin className="size-3.5 text-[#1a8a9d]" />
              <span>Station Pickup</span>
            </div>
            <p className="text-[10px] text-muted-foreground mt-0.5">Free pickup at station</p>
          </button>

          <button
            type="button"
            onClick={() => onDeliveryTypeChange('DOORSTEP')}
            className={`p-3 rounded-xl border text-left transition-all min-h-[44px] ${
              deliveryType === 'DOORSTEP'
                ? 'border-[#1a8a9d] bg-[#1a8a9d]/10 text-foreground font-bold'
                : 'border-border bg-muted/30 text-muted-foreground hover:bg-muted/50'
            }`}
          >
            <div className="flex items-center gap-1.5 text-xs">
              <span>🏠</span>
              <span>Doorstep Delivery</span>
            </div>
            <p className="text-[10px] text-muted-foreground mt-0.5">+100,000 VND delivery fee</p>
          </button>
        </div>
      </div>

      {deliveryType === 'DOORSTEP' && (
        <div>
          <label className="block text-[11px] font-semibold text-muted-foreground mb-1">Delivery Address</label>
          <input
            type="text"
            placeholder="e.g. Caravelle Hotel, 19 Lam Son Square, District 1"
            value={deliveryAddress}
            onChange={(e) => onDeliveryAddressChange(e.target.value)}
            required={isLoggedIn}
            className="w-full h-11 rounded-xl border border-border bg-muted/40 px-3 text-xs text-foreground focus:outline-none focus:border-[#1a8a9d]"
          />
        </div>
      )}
    </>
  )
}

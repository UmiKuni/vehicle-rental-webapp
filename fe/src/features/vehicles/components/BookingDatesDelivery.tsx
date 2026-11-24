import { Calendar, MapPin } from 'lucide-react'

interface BookingDatesDeliveryProps {
  startDate: string
  endDate: string
  tomorrow: string
  pickupLocation: string
  onStartDateChange: (value: string) => void
  onEndDateChange: (value: string) => void
  onPickupLocationChange: (value: string) => void
}

export function BookingDatesDelivery({
  startDate,
  endDate,
  tomorrow,
  pickupLocation,
  onStartDateChange,
  onEndDateChange,
  onPickupLocationChange,
}: BookingDatesDeliveryProps) {
  return (
    <>
      <div className="grid grid-cols-2 gap-3">
        <label className="block text-[11px] font-semibold text-muted-foreground">
          Pick-up date
          <span className="relative mt-1 block">
            <Calendar className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <input type="date" value={startDate} min={tomorrow} onChange={(event) => onStartDateChange(event.target.value)} className="h-11 w-full rounded-xl border border-border bg-muted/40 pl-9 pr-2 text-xs font-medium text-foreground focus:border-[#1a8a9d] focus:outline-none" />
          </span>
        </label>
        <label className="block text-[11px] font-semibold text-muted-foreground">
          Return date
          <span className="relative mt-1 block">
            <Calendar className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <input type="date" value={endDate} min={startDate} onChange={(event) => onEndDateChange(event.target.value)} className="h-11 w-full rounded-xl border border-border bg-muted/40 pl-9 pr-2 text-xs font-medium text-foreground focus:border-[#1a8a9d] focus:outline-none" />
          </span>
        </label>
      </div>
      <label className="block text-[11px] font-semibold text-muted-foreground">
        Pick-up location
        <span className="relative mt-1 block">
          <MapPin className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input type="text" value={pickupLocation} onChange={(event) => onPickupLocationChange(event.target.value)} required maxLength={240} placeholder="Vehicle rental station or agreed address" className="h-11 w-full rounded-xl border border-border bg-muted/40 pl-9 pr-3 text-xs text-foreground focus:border-[#1a8a9d] focus:outline-none" />
        </span>
        <span className="mt-1 block font-normal text-muted-foreground">This location is saved with the booking for the owner to coordinate.</span>
      </label>
    </>
  )
}

import { Star, MapPin, Users, Fuel, Gauge, Sparkles } from 'lucide-react'
import type { Vehicle } from '@/types/vehicle.types'

interface VehicleCardProps {
  vehicle: Vehicle
  onBook: (vehicle: Vehicle) => void
}

const CATEGORY_NAMES: Record<string, string> = {
  ELECTRIC: 'Xe điện',
  CAR_4_SEAT: '4-5 chỗ',
  CAR_7_SEAT: '7 chỗ',
  TRUCK: 'Bán tải',
  MOTORBIKE: 'Xe máy',
}

export function VehicleCard({ vehicle, onBook }: VehicleCardProps) {
  const formattedPrice = new Intl.NumberFormat('vi-VN').format(vehicle.pricePerDay)

  return (
    <div className="overflow-hidden rounded-2xl border border-border/80 bg-card text-card-foreground shadow-xs hover:shadow-md transition-all">
      {/* Vehicle Image with Aspect Ratio & Badges */}
      <div className="relative aspect-[16/10] w-full overflow-hidden bg-muted">
        <img
          src={vehicle.imageUrl}
          alt={vehicle.name}
          className="h-full w-full object-cover transition-transform duration-300 hover:scale-105"
          loading="lazy"
        />

        {/* Top Badges */}
        <div className="absolute top-2.5 inset-x-2.5 flex items-center justify-between">
          <span className="inline-flex items-center gap-1 rounded-full bg-black/60 backdrop-blur-md px-2.5 py-1 text-[11px] font-bold text-white shadow-xs">
            <Sparkles className="size-3 text-[#22d3ee]" />
            {CATEGORY_NAMES[vehicle.category] ?? vehicle.category}
          </span>
          <span className="inline-flex items-center gap-1 rounded-full bg-white/90 dark:bg-zinc-900/90 backdrop-blur-md px-2 py-0.5 text-[11px] font-bold text-foreground shadow-xs">
            <Star className="size-3 fill-amber-400 text-amber-400" />
            {vehicle.rating}
            <span className="text-[10px] text-muted-foreground font-normal">({vehicle.tripsCount})</span>
          </span>
        </div>

        {/* Fuel / Eco Badge Bottom Right */}
        <div className="absolute bottom-2.5 right-2.5">
          <span
            className={`rounded-md px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider shadow-xs ${
              vehicle.fuelType === 'ELECTRIC'
                ? 'bg-emerald-500 text-white'
                : 'bg-zinc-900/80 text-zinc-200'
            }`}
          >
            {vehicle.fuelType === 'ELECTRIC' ? '⚡ Điện' : vehicle.fuelType === 'DIESEL' ? 'Dầu' : 'Xăng'}
          </span>
        </div>
      </div>

      {/* Content */}
      <div className="p-3.5 space-y-3">
        {/* Name & Location */}
        <div>
          <div className="flex items-start justify-between gap-2">
            <h3 className="font-bold text-foreground text-sm sm:text-base leading-snug line-clamp-1">
              {vehicle.name}
            </h3>
            <span className="text-[11px] font-semibold text-muted-foreground shrink-0 mt-0.5">
              {vehicle.modelYear}
            </span>
          </div>
          <p className="mt-1 flex items-center gap-1 text-[11px] text-muted-foreground truncate">
            <MapPin className="size-3 text-[#1a8a9d] shrink-0" />
            <span>{vehicle.location}</span>
          </p>
        </div>

        {/* Specs Row */}
        <div className="grid grid-cols-3 gap-1 rounded-xl bg-muted/50 p-2 text-[11px] text-muted-foreground">
          <div className="flex items-center gap-1.5 justify-center">
            <Users className="size-3.5 text-foreground/70" />
            <span>{vehicle.seats} chỗ</span>
          </div>
          <div className="flex items-center gap-1.5 justify-center border-x border-border/60">
            <Gauge className="size-3.5 text-foreground/70" />
            <span>{vehicle.transmission === 'AUTOMATIC' ? 'Tự động' : 'Số sàn'}</span>
          </div>
          <div className="flex items-center gap-1.5 justify-center">
            <Fuel className="size-3.5 text-foreground/70" />
            <span className="truncate">{vehicle.brand}</span>
          </div>
        </div>

        {/* Features Chips */}
        <div className="flex flex-wrap gap-1">
          {vehicle.features.slice(0, 3).map((f) => (
            <span
              key={f}
              className="rounded-md border border-border/60 bg-background px-1.5 py-0.5 text-[10px] text-muted-foreground"
            >
              {f}
            </span>
          ))}
        </div>

        {/* Footer: Price & CTA */}
        <div className="flex items-center justify-between pt-1 border-t border-border/50">
          <div>
            <div className="flex items-baseline gap-1">
              <span className="text-base sm:text-lg font-extrabold text-[#1a8a9d] dark:text-[#22d3ee]">
                {formattedPrice}đ
              </span>
              <span className="text-[10px] text-muted-foreground font-medium">/ ngày</span>
            </div>
          </div>

          <button
            type="button"
            onClick={() => onBook(vehicle)}
            className="min-h-[44px] min-w-[100px] rounded-xl bg-[#1a8a9d] px-4 py-2 text-xs font-bold text-white shadow-xs hover:bg-[#147485] active-press transition-colors"
          >
            Đặt xe ngay
          </button>
        </div>
      </div>
    </div>
  )
}

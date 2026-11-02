import { Search, X } from 'lucide-react'
import type { VehicleInterest } from '@/types/kyc.types'
import type { TransmissionType } from '@/types/vehicle.types'

interface VehicleFilterBarProps {
  search: string
  onSearchChange: (search: string) => void
  selectedCategory: VehicleInterest | 'ALL'
  onCategoryChange: (cat: VehicleInterest | 'ALL') => void
  selectedTransmission: TransmissionType | 'ALL'
  onTransmissionChange: (trans: TransmissionType | 'ALL') => void
}

const CATEGORIES: { id: VehicleInterest | 'ALL'; label: string; icon: string }[] = [
  { id: 'ALL',        label: 'All',          icon: '✨' },
  { id: 'ELECTRIC',   label: 'Electric',     icon: '⚡' },
  { id: 'CAR_4_SEAT', label: '4-5 Seats',    icon: '🚗' },
  { id: 'CAR_7_SEAT', label: '7 Seats',      icon: '🚐' },
  { id: 'TRUCK',      label: 'Pickup Truck', icon: '🛻' },
  { id: 'MOTORBIKE',  label: 'Motorbike',    icon: '🛵' },
]

export function VehicleFilterBar({
  search,
  onSearchChange,
  selectedCategory,
  onCategoryChange,
  selectedTransmission,
  onTransmissionChange,
}: VehicleFilterBarProps) {
  return (
    <div className="space-y-3 sticky top-0 z-30 bg-background/95 backdrop-blur-md pt-2 pb-3 px-4 border-b border-border/60">
      {/* Search Input Box */}
      <div className="relative">
        <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
        <input
          type="text"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="Search by vehicle name, brand or district..."
          className="w-full h-11 rounded-2xl border border-border bg-muted/40 pl-10 pr-9 text-xs font-medium text-foreground placeholder:text-muted-foreground focus:bg-background focus:border-[#1a8a9d] focus:outline-none focus:ring-1 focus:ring-[#1a8a9d] transition-all"
        />
        {search && (
          <button
            type="button"
            onClick={() => onSearchChange('')}
            className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-full p-1 text-muted-foreground hover:text-foreground"
          >
            <X className="size-3.5" />
          </button>
        )}
      </div>

      {/* Category Chips (Horizontally Scrollable) */}
      <div className="flex items-center gap-1.5 overflow-x-auto no-scrollbar py-0.5">
        {CATEGORIES.map(({ id, label, icon }) => {
          const isActive = selectedCategory === id
          return (
            <button
              key={id}
              type="button"
              onClick={() => onCategoryChange(id)}
              className={`shrink-0 min-h-[36px] rounded-xl px-3 py-1.5 text-xs font-bold transition-all flex items-center gap-1.5 active-press ${
                isActive
                  ? 'bg-[#1a8a9d] text-white shadow-xs'
                  : 'bg-card text-muted-foreground border border-border/80 hover:bg-muted/60 hover:text-foreground'
              }`}
            >
              <span>{icon}</span>
              <span>{label}</span>
            </button>
          )
        })}
      </div>

      {/* Transmission Quick Filter */}
      <div className="flex items-center gap-1.5 text-[11px]">
        <span className="text-muted-foreground font-medium shrink-0">Transmission:</span>
        {(['ALL', 'AUTOMATIC', 'MANUAL'] as const).map((trans) => (
          <button
            key={trans}
            type="button"
            onClick={() => onTransmissionChange(trans)}
            className={`rounded-lg px-2.5 py-1 font-semibold transition-colors ${
              selectedTransmission === trans
                ? 'bg-foreground text-background font-bold'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            {trans === 'ALL' ? 'All' : trans === 'AUTOMATIC' ? 'Automatic' : 'Manual'}
          </button>
        ))}
      </div>
    </div>
  )
}

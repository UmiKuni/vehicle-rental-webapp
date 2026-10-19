import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Car, Sparkles, FilterX } from 'lucide-react'
import type { Vehicle, VehicleFilterParams, TransmissionType } from '@/types/vehicle.types'
import type { VehicleInterest } from '@/types/kyc.types'
import { useVehicles } from '../hooks/useVehicles'
import { VehicleCard } from '../components/VehicleCard'
import { VehicleFilterBar } from '../components/VehicleFilterBar'
import { BookingModal } from '../components/BookingModal'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ROUTES } from '@/shared/constants'

export default function SearchVehiclesPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [selectedCategory, setSelectedCategory] = useState<VehicleInterest | 'ALL'>('ALL')
  const [selectedTransmission, setSelectedTransmission] = useState<TransmissionType | 'ALL'>('ALL')
  const [selectedVehicleForBooking, setSelectedVehicleForBooking] = useState<Vehicle | null>(null)

  const filterParams: VehicleFilterParams = {
    search: search.trim() || undefined,
    category: selectedCategory,
    transmission: selectedTransmission,
  }

  const { data: vehicles = [], isLoading } = useVehicles(filterParams)

  return (
    <div className="space-y-4 pb-20">
      {/* Top Filter Bar */}
      <VehicleFilterBar
        search={search}
        onSearchChange={setSearch}
        selectedCategory={selectedCategory}
        onCategoryChange={setSelectedCategory}
        selectedTransmission={selectedTransmission}
        onTransmissionChange={setSelectedTransmission}
      />

      {/* Main Content Area */}
      <div className="px-4 space-y-3">
        {/* Results Count & Quick Promo Banner */}
        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <div className="flex items-center gap-1.5 font-bold text-foreground">
            <Car className="size-4 text-[#1a8a9d]" />
            <span>{vehicles.length} dòng xe sẵn sàng đón bạn</span>
          </div>
          <span className="flex items-center gap-1 text-[11px] text-[#1a8a9d] font-semibold">
            <Sparkles className="size-3" />
            Xe tự lái 100%
          </span>
        </div>

        {/* Loading State */}
        {isLoading && (
          <div className="grid grid-cols-1 gap-4">
            <CardSkeleton />
            <CardSkeleton />
          </div>
        )}

        {/* Empty State */}
        {!isLoading && vehicles.length === 0 && (
          <div className="flex flex-col items-center justify-center py-16 text-center px-4">
            <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-muted text-muted-foreground mb-3">
              <FilterX className="size-7" />
            </div>
            <h3 className="font-bold text-foreground text-sm">Không tìm thấy xe phù hợp</h3>
            <p className="mt-1 text-xs text-muted-foreground max-w-xs">
              Thử xóa từ khóa tìm kiếm hoặc chọn loại xe khác để xem thêm kết quả.
            </p>
            <button
              type="button"
              onClick={() => {
                setSearch('')
                setSelectedCategory('ALL')
                setSelectedTransmission('ALL')
              }}
              className="mt-4 rounded-xl border border-border bg-card px-4 py-2 text-xs font-semibold hover:bg-muted"
            >
              Đặt lại bộ lọc
            </button>
          </div>
        )}

        {/* Vehicle Grid */}
        {!isLoading && vehicles.length > 0 && (
          <div className="grid grid-cols-1 gap-4">
            {vehicles.map((v) => (
              <VehicleCard
                key={v.id}
                vehicle={v}
                onBook={(car) => setSelectedVehicleForBooking(car)}
              />
            ))}
          </div>
        )}
      </div>

      {/* Booking Modal */}
      <BookingModal
        vehicle={selectedVehicleForBooking}
        onClose={() => setSelectedVehicleForBooking(null)}
        onSuccess={() => navigate(ROUTES.BOOKINGS)}
      />
    </div>
  )
}

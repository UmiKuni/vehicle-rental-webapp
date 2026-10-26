import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { LocateFixed, Map, MapPin, Search } from 'lucide-react'
import type { Vehicle, VehicleFilterParams } from '@/types/vehicle.types'
import { useVehicles } from '../hooks/useVehicles'
import { VehicleCard } from '../components/VehicleCard'
import { VehicleFilterBar } from '../components/VehicleFilterBar'
import { BookingModal } from '../components/BookingModal'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ErrorState } from '@/shared/components/common/StateViews'
import { ROUTES } from '@/shared/constants'

const pinPositions = [
  { left: '19%', top: '31%' }, { left: '44%', top: '23%' }, { left: '70%', top: '38%' }, { left: '31%', top: '57%' },
  { left: '58%', top: '63%' }, { left: '80%', top: '72%' }, { left: '52%', top: '44%' }, { left: '14%', top: '76%' },
]

export default function SearchVehiclesPage() {
  const navigate = useNavigate()
  const [filters, setFilters] = useState<VehicleFilterParams>({ category: 'ALL', transmission: 'ALL' })
  const [location, setLocation] = useState('Đang lấy vị trí hiện tại...')
  const [locating, setLocating] = useState(false)
  const [isExpanded, setIsExpanded] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedVehicleForBooking, setSelectedVehicleForBooking] = useState<Vehicle | null>(null)
  const { data: vehicles = [], isLoading, isError, refetch } = useVehicles(filters)

  const sortedVehicles = useMemo(() => vehicles, [vehicles])
  const selectedVehicle = sortedVehicles.find((vehicle) => vehicle.id === selectedId) ?? sortedVehicles[0]

  const locate = () => {
    if (!navigator.geolocation) {
      setLocation('TP. Hồ Chí Minh')
      return
    }
    setLocating(true)
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        setLocation(`Vị trí của tôi · ${coords.latitude.toFixed(3)}, ${coords.longitude.toFixed(3)}`)
        setFilters((current) => ({ ...current, location: undefined }))
        setLocating(false)
      },
      () => {
        setLocation('TP. Hồ Chí Minh')
        setFilters((current) => ({ ...current, location: undefined }))
        setLocating(false)
      },
      { enableHighAccuracy: false, timeout: 8000, maximumAge: 300000 }
    )
  }

  const changeLocation = (value: string) => {
    setLocation(value)
    setFilters((current) => ({ ...current, location: value.trim() || undefined }))
  }

  useEffect(() => { locate() }, [])
  useEffect(() => {
    if (!selectedId && sortedVehicles.length) setSelectedId(sortedVehicles[0].id)
    else if (selectedId && !sortedVehicles.some((vehicle) => vehicle.id === selectedId)) setSelectedId(sortedVehicles[0]?.id ?? null)
  }, [sortedVehicles, selectedId])

  const handleTouchStart = (event: React.TouchEvent) => { (event.currentTarget as HTMLElement).dataset.touchY = String(event.touches[0].clientY) }
  const handleTouchEnd = (event: React.TouchEvent) => {
    const start = Number((event.currentTarget as HTMLElement).dataset.touchY ?? 0)
    const delta = event.changedTouches[0].clientY - start
    if (delta < -45) setIsExpanded(true)
    if (delta > 45) setIsExpanded(false)
  }

  return (
    <div className={`vehicle-search-page ${isExpanded ? 'is-expanded' : ''}`}>
      <VehicleFilterBar location={location} onLocationChange={changeLocation} onLocate={locate} locating={locating} filters={filters} onApply={setFilters} />
      <div className="search-map-stage">
        <div className="map-canvas" aria-label="Bản đồ kết quả tìm kiếm">
          <div className="map-water" />
          <div className="map-park park-one" /><div className="map-park park-two" /><div className="map-park park-three" />
          <div className="map-block block-one" /><div className="map-block block-two" /><div className="map-block block-three" />
          <div className="map-road road-horizontal road-a" /><div className="map-road road-horizontal road-b" /><div className="map-road road-horizontal road-c" />
          <div className="map-road road-vertical road-d" /><div className="map-road road-vertical road-e" /><div className="map-road road-vertical road-f" />
          <div className="map-road road-diagonal" />
          <span className="map-label label-one">QUẬN 1</span><span className="map-label label-two">BÌNH THẠNH</span><span className="map-label label-three">THỦ THIÊM</span><span className="map-river-label">SÔNG SÀI GÒN</span>
          <div className="map-current-location"><LocateFixed size={14} /></div>
          {sortedVehicles.map((vehicle, index) => {
            const position = pinPositions[index % pinPositions.length]
            const active = vehicle.id === selectedVehicle?.id
            return <button key={vehicle.id} type="button" className={`vehicle-map-pin ${active ? 'active' : ''}`} style={position} onClick={() => { setSelectedId(vehicle.id); setIsExpanded(false) }} aria-label={`Chọn ${vehicle.name}`}><MapPin size={active ? 16 : 13} fill="currentColor" /><span>{new Intl.NumberFormat('vi-VN', { notation: 'compact' }).format(vehicle.pricePerDay)}₫</span></button>
          })}
          <div className="map-attribution"><Map size={12} /> Bản đồ khu vực · Kết quả xe</div>
        </div>
      </div>

      <section className="search-results-sheet" aria-label="Danh sách xe tìm được">
        <button type="button" className="sheet-handle-area" onClick={() => setIsExpanded((value) => !value)} onTouchStart={handleTouchStart} onTouchEnd={handleTouchEnd} aria-label={isExpanded ? 'Thu gọn danh sách xe' : 'Mở rộng danh sách xe'} aria-expanded={isExpanded}>
          <span className="sheet-handle" /><span className="sheet-handle-text">{isExpanded ? 'Thu gọn' : 'Vuốt lên để xem danh sách'}</span>
        </button>
        <div className="search-results-heading"><div><span className="search-results-kicker">KẾT QUẢ GẦN BẠN</span><h1>{isLoading ? 'Đang tìm xe…' : `${vehicles.length} xe phù hợp`}</h1></div><span className="search-results-location"><MapPin size={13} /> {location.startsWith('Vị trí') ? 'Gần bạn' : location}</span></div>
        <div className="sheet-content">
          {isLoading && <div className="grid gap-3"><CardSkeleton /><CardSkeleton /></div>}
          {isError && <ErrorState message="Không tải được danh sách xe." onRetry={() => void refetch()} />}
          {!isLoading && !isError && vehicles.length === 0 && <div className="search-empty"><Search size={24} /><strong>Chưa tìm thấy xe phù hợp</strong><span>Thử thay đổi bộ lọc hoặc khu vực tìm kiếm.</span><button type="button" onClick={() => setFilters({ category: 'ALL', transmission: 'ALL' })}>Xóa bộ lọc</button></div>}
          {!isLoading && !isError && vehicles.length > 0 && (
            <div className={isExpanded ? 'search-vehicle-list' : 'search-featured-vehicle'}>
              {(isExpanded ? sortedVehicles : selectedVehicle ? [selectedVehicle] : []).map((vehicle) => <VehicleCard key={vehicle.id} vehicle={vehicle} onBook={setSelectedVehicleForBooking} onViewDetails={(car) => navigate(`/vehicles/${car.id}`)} />)}
            </div>
          )}
        </div>
      </section>
      <BookingModal vehicle={selectedVehicleForBooking} onClose={() => setSelectedVehicleForBooking(null)} onSuccess={() => navigate(ROUTES.BOOKINGS)} />
    </div>
  )
}

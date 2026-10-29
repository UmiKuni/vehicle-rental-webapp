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
import { reverseGeocode } from '../lib/reverseGeocode'

const pinPositions = [
  { left: '19%', top: '31%' }, { left: '44%', top: '23%' }, { left: '70%', top: '38%' }, { left: '31%', top: '57%' },
  { left: '58%', top: '63%' }, { left: '80%', top: '72%' }, { left: '52%', top: '44%' }, { left: '14%', top: '76%' },
]

export default function SearchVehiclesPage() {
  const navigate = useNavigate()
  const [filters, setFilters] = useState<VehicleFilterParams>({ category: 'ALL', transmission: 'ALL' })
  const [location, setLocation] = useState('Finding your location...')
  const [locationFromGoogle, setLocationFromGoogle] = useState(false)
  const [locating, setLocating] = useState(false)
  const [isExpanded, setIsExpanded] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedVehicleForBooking, setSelectedVehicleForBooking] = useState<Vehicle | null>(null)
  const { data: vehicles = [], isLoading, isError, refetch } = useVehicles(filters)

  const sortedVehicles = useMemo(() => vehicles, [vehicles])
  const selectedVehicle = sortedVehicles.find((vehicle) => vehicle.id === selectedId) ?? sortedVehicles[0]

  const locate = () => {
    if (!navigator.geolocation) {
      setLocation('Ho Chi Minh City')
      setLocationFromGoogle(false)
      return
    }
    setLocating(true)
    navigator.geolocation.getCurrentPosition(
      async ({ coords }) => {
        setFilters((current) => ({ ...current, location: undefined }))
        try {
          setLocation(await reverseGeocode(coords.latitude, coords.longitude))
          setLocationFromGoogle(true)
        } catch {
          setLocation('Near your current location')
          setLocationFromGoogle(false)
        } finally {
          setLocating(false)
        }
      },
      () => {
        setLocation('Ho Chi Minh City')
        setLocationFromGoogle(false)
        setFilters((current) => ({ ...current, location: undefined }))
        setLocating(false)
      },
      { enableHighAccuracy: false, timeout: 8000, maximumAge: 300000 }
    )
  }

  const changeLocation = (value: string) => {
    setLocation(value)
    setLocationFromGoogle(false)
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
      <VehicleFilterBar location={location} locationFromGoogle={locationFromGoogle} onLocationChange={changeLocation} onLocate={locate} locating={locating} filters={filters} onApply={setFilters} />
      <div className="search-map-stage">
        <div className="map-canvas" aria-label="Map of matching vehicles">
          <div className="map-water" />
          <div className="map-park park-one" /><div className="map-park park-two" /><div className="map-park park-three" />
          <div className="map-block block-one" /><div className="map-block block-two" /><div className="map-block block-three" />
          <div className="map-road road-horizontal road-a" /><div className="map-road road-horizontal road-b" /><div className="map-road road-horizontal road-c" />
          <div className="map-road road-vertical road-d" /><div className="map-road road-vertical road-e" /><div className="map-road road-vertical road-f" />
          <div className="map-road road-diagonal" />
          <span className="map-label label-one">DISTRICT 1</span><span className="map-label label-two">BINH THANH</span><span className="map-label label-three">THU THIEM</span><span className="map-river-label">SAIGON RIVER</span>
          <div className="map-current-location"><LocateFixed size={14} /></div>
          {sortedVehicles.map((vehicle, index) => {
            const position = pinPositions[index % pinPositions.length]
            const active = vehicle.id === selectedVehicle?.id
            return <button key={vehicle.id} type="button" className={`vehicle-map-pin ${active ? 'active' : ''}`} style={position} onClick={() => { setSelectedId(vehicle.id); setIsExpanded(false) }} aria-label={`Select ${vehicle.name}`}><MapPin size={active ? 16 : 13} fill="currentColor" /><span>{new Intl.NumberFormat('en-US', { notation: 'compact' }).format(vehicle.pricePerDay)} VND</span></button>
          })}
          <div className="map-attribution"><Map size={12} /> Area map · Vehicle results</div>
        </div>
      </div>

      <section className="search-results-sheet" aria-label="Search results">
        <button type="button" className="sheet-handle-area" onClick={() => setIsExpanded((value) => !value)} onTouchStart={handleTouchStart} onTouchEnd={handleTouchEnd} aria-label={isExpanded ? 'Collapse vehicle list' : 'Expand vehicle list'} aria-expanded={isExpanded}>
          <span className="sheet-handle" /><span className="sheet-handle-text">{isExpanded ? 'Collapse list' : 'Swipe up to view all cars'}</span>
        </button>
        <div className="search-results-heading"><div><span className="search-results-kicker">AVAILABLE NEAR YOU</span><h1>{isLoading ? 'Finding cars…' : `${vehicles.length} cars found`}</h1></div><span className="search-results-location"><MapPin size={13} /> {location.startsWith('Near your current') ? 'Near you' : location}</span></div>
        <div className="sheet-content">
          {isLoading && <div className="grid gap-3"><CardSkeleton /><CardSkeleton /></div>}
          {isError && <ErrorState message="Could not load vehicles." onRetry={() => void refetch()} />}
          {!isLoading && !isError && vehicles.length === 0 && <div className="search-empty"><Search size={24} /><strong>No matching cars found</strong><span>Try changing your filters or search area.</span><button type="button" onClick={() => setFilters({ category: 'ALL', transmission: 'ALL' })}>Clear filters</button></div>}
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

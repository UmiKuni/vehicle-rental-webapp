import { useCallback, useEffect, useMemo, useState } from 'react'
import { useLocation as useRouterLocation, useNavigate } from 'react-router-dom'
import { Search } from 'lucide-react'
import type { Vehicle, VehicleFilterParams } from '@/types/vehicle.types'
import { useVehicles } from '../hooks/useVehicles'
import { VehicleCard } from '../components/VehicleCard'
import { VehicleFilterBar } from '../components/VehicleFilterBar'
import { GoongVehicleMap } from '../components/GoongVehicleMap'
import { BookingModal } from '../components/BookingModal'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ErrorState } from '@/shared/components/common/StateViews'
import { reverseGeocode } from '../lib/reverseGeocode'
import { useDebounce } from '@/shared/hooks/useDebounce'

export default function SearchVehiclesPage() {
  const navigate = useNavigate()
  const routerLocation = useRouterLocation()
  const [hasSavedFilters] = useState(() => {
    try { return Boolean(sessionStorage.getItem('vehicle-search-filters')) } catch { return false }
  })
  const [filters, setFilters] = useState<VehicleFilterParams>(() => {
    try {
      const saved = sessionStorage.getItem('vehicle-search-filters')
      if (!saved) return { category: 'ALL', transmission: 'ALL' }
      const parsed = JSON.parse(saved) as VehicleFilterParams
      return {
        category: 'ALL',
        transmission: 'ALL',
        ...parsed,
        pickupAt: parsed.pickupAt?.slice(0, 10),
        returnAt: parsed.returnAt?.slice(0, 10),
      }
    } catch {
      return { category: 'ALL', transmission: 'ALL' }
    }
  })
  const [location, setLocation] = useState(() => {
    try { return filters.latitude != null && filters.longitude != null ? sessionStorage.getItem('vehicle-search-location') ?? 'Near your current location' : 'All available locations' } catch { return 'All available locations' }
  })
  const [locationFromGoogle, setLocationFromGoogle] = useState(false)
  const [locating, setLocating] = useState(false)
  const [isExpanded, setIsExpanded] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedVehicleForBooking, setSelectedVehicleForBooking] = useState<Vehicle | null>(null)
  const debouncedSearch = useDebounce(filters.search, 300)
  const queryFilters = { ...filters, search: debouncedSearch }
  const { data: vehicles = [], isLoading, isError, refetch } = useVehicles(queryFilters)

  const sortedVehicles = useMemo(() => vehicles, [vehicles])
  const selectedVehicle = sortedVehicles.find((vehicle) => vehicle.id === selectedId) ?? sortedVehicles[0]
  const currentMapLocation = useMemo(() => (
    filters.latitude != null && filters.longitude != null
      ? { latitude: filters.latitude, longitude: filters.longitude }
      : undefined
  ), [filters.latitude, filters.longitude])

  const locate = useCallback(() => {
    if (!navigator.geolocation) {
      setLocation('All available locations')
      setLocationFromGoogle(false)
      setFilters((current) => ({ ...current, location: undefined, latitude: undefined, longitude: undefined }))
      return
    }
    setLocating(true)
    navigator.geolocation.getCurrentPosition(
      async ({ coords }) => {
        setFilters((current) => ({ ...current, location: undefined, latitude: coords.latitude, longitude: coords.longitude }))
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
        setLocation('All available locations')
        setLocationFromGoogle(false)
        setFilters((current) => ({ ...current, location: undefined, latitude: undefined, longitude: undefined }))
        setLocating(false)
      },
      { enableHighAccuracy: false, timeout: 8000, maximumAge: 300000 }
    )
  }, [])

  useEffect(() => {
    if (hasSavedFilters && location !== 'Finding your location...') return
    const timer = window.setTimeout(locate, 0)
    return () => window.clearTimeout(timer)
  }, [hasSavedFilters, location, locate])
  useEffect(() => {
    try { sessionStorage.setItem('vehicle-search-filters', JSON.stringify(filters)) } catch { /* Search still works without storage access. */ }
  }, [filters])
  useEffect(() => {
    try { sessionStorage.setItem('vehicle-search-location', location) } catch { /* Search still works without storage access. */ }
  }, [location])
  useEffect(() => {
    const state = routerLocation.state as { resumeBookingVehicleId?: string; resumeBookingVehicle?: Vehicle; pickupAt?: string; returnAt?: string } | null
    if (!state?.resumeBookingVehicleId) return
    const vehicle = sortedVehicles.find((item) => item.id === state.resumeBookingVehicleId)
      ?? (state.resumeBookingVehicle?.id === state.resumeBookingVehicleId ? state.resumeBookingVehicle : undefined)
    if (!vehicle) return
    const timer = window.setTimeout(() => {
      setSelectedVehicleForBooking(vehicle)
      setFilters((current) => ({ ...current, pickupAt: state.pickupAt?.slice(0, 10), returnAt: state.returnAt?.slice(0, 10) }))
      navigate(`${routerLocation.pathname}${routerLocation.search}`, {
        replace: true,
        state: { pickupAt: state.pickupAt, returnAt: state.returnAt },
      })
    }, 0)
    return () => window.clearTimeout(timer)
  }, [sortedVehicles, routerLocation.state, routerLocation.pathname, routerLocation.search, navigate])

  const handleTouchStart = (event: React.TouchEvent) => { (event.currentTarget as HTMLElement).dataset.touchY = String(event.touches[0].clientY) }
  const handleTouchEnd = (event: React.TouchEvent) => {
    const start = Number((event.currentTarget as HTMLElement).dataset.touchY ?? 0)
    const delta = event.changedTouches[0].clientY - start
    if (delta < -45) setIsExpanded(true)
    if (delta > 45) setIsExpanded(false)
  }

  return (
    <div className={`vehicle-search-page ${isExpanded ? 'is-expanded' : ''}`}>
      <VehicleFilterBar location={location} locationFromGoogle={locationFromGoogle} onLocate={locate} locating={locating} filters={filters} onApply={setFilters} />
      <div className="search-map-stage">
        <GoongVehicleMap
          vehicles={sortedVehicles}
          selectedVehicleId={selectedVehicle?.id}
          currentLocation={currentMapLocation}
          onSelect={(vehicle) => {
            setSelectedId(vehicle.id)
            setIsExpanded(false)
          }}
        />
      </div>

      <section className="search-results-sheet" aria-label="Search results">
        <button type="button" className="sheet-handle-area" onClick={() => setIsExpanded((value) => !value)} onTouchStart={handleTouchStart} onTouchEnd={handleTouchEnd} aria-label={isExpanded ? 'Collapse vehicle list' : 'Expand vehicle list'} aria-expanded={isExpanded}>
          <span className="sheet-handle" />
        </button>
        <div className="search-results-heading"><div><h1>{isLoading ? 'Finding cars…' : `${vehicles.length} results found`}</h1></div></div>
        <div className="sheet-content">
          {isLoading && <div className="grid gap-3"><CardSkeleton /><CardSkeleton /></div>}
          {isError && <ErrorState message="Could not load vehicles." onRetry={() => void refetch()} />}
          {!isLoading && !isError && vehicles.length === 0 && <div className="search-empty"><Search size={24} /><strong>No matching cars found</strong><span>Try changing your filters or search area.</span><button type="button" onClick={() => setFilters({ category: 'ALL', transmission: 'ALL' })}>Clear filters</button></div>}
          {!isLoading && !isError && vehicles.length > 0 && (
            <div className={isExpanded ? 'search-vehicle-list' : 'search-featured-vehicle'}>
              {(isExpanded ? sortedVehicles : selectedVehicle ? [selectedVehicle] : []).map((vehicle) => <VehicleCard key={vehicle.id} vehicle={vehicle} onBook={setSelectedVehicleForBooking} onViewDetails={(car) => navigate(`/vehicles/${car.id}`, { state: { pickupAt: filters.pickupAt, returnAt: filters.returnAt } })} />)}
            </div>
          )}
        </div>
      </section>
      <BookingModal key={selectedVehicleForBooking ? `${selectedVehicleForBooking.id}-${filters.pickupAt ?? ''}-${filters.returnAt ?? ''}` : 'closed'} vehicle={selectedVehicleForBooking} onClose={() => setSelectedVehicleForBooking(null)} onSuccess={(booking) => navigate(`/booking/${booking.id}`)} initialStartDate={filters.pickupAt} initialEndDate={filters.returnAt} />
    </div>
  )
}

import { useCallback, useEffect, useMemo, useState } from 'react'
import { useLocation as useRouterLocation, useNavigate } from 'react-router-dom'
import type { Vehicle, VehicleFilterParams } from '@/types/vehicle.types'
import { useVehicles } from '../hooks/useVehicles'
import { VehicleFilterBar } from '../components/VehicleFilterBar'
import { GoongVehicleMap } from '../components/GoongVehicleMap'
import { SearchResultsSheet } from '../components/SearchResultsSheet'
import { BookingModal } from '../components/BookingModal'
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

      <SearchResultsSheet
        vehicles={sortedVehicles}
        selectedVehicle={selectedVehicle}
        expanded={isExpanded}
        loading={isLoading}
        error={isError}
        onExpandedChange={setIsExpanded}
        onRetry={() => void refetch()}
        onClearFilters={() => setFilters({ category: 'ALL', transmission: 'ALL' })}
        onBook={setSelectedVehicleForBooking}
        onViewDetails={(car) => navigate(`/vehicles/${car.id}`, { state: { pickupAt: filters.pickupAt, returnAt: filters.returnAt } })}
      />
      <BookingModal key={selectedVehicleForBooking ? `${selectedVehicleForBooking.id}-${filters.pickupAt ?? ''}-${filters.returnAt ?? ''}` : 'closed'} vehicle={selectedVehicleForBooking} onClose={() => setSelectedVehicleForBooking(null)} onSuccess={(booking) => navigate(`/booking/${booking.id}`)} initialStartDate={filters.pickupAt} initialEndDate={filters.returnAt} />
    </div>
  )
}

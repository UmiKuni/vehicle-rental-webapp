import { useCallback, useEffect, useMemo, useState } from 'react'
import { useLocation as useRouterLocation, useNavigate } from 'react-router-dom'
import { LocateFixed, Map, MapPin, Minus, Plus, RotateCcw, Search } from 'lucide-react'
import type { Vehicle, VehicleFilterParams } from '@/types/vehicle.types'
import { useVehicles } from '../hooks/useVehicles'
import { VehicleCard } from '../components/VehicleCard'
import { VehicleFilterBar } from '../components/VehicleFilterBar'
import { BookingModal } from '../components/BookingModal'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ErrorState } from '@/shared/components/common/StateViews'
import { reverseGeocode } from '../lib/reverseGeocode'

const pinPositions = [
  { left: '19%', top: '31%' }, { left: '44%', top: '23%' }, { left: '70%', top: '38%' }, { left: '31%', top: '57%' },
  { left: '58%', top: '63%' }, { left: '80%', top: '72%' }, { left: '52%', top: '44%' }, { left: '14%', top: '76%' },
]

export default function SearchVehiclesPage() {
  const navigate = useNavigate()
  const routerLocation = useRouterLocation()
  const [hasSavedFilters] = useState(() => {
    try { return Boolean(sessionStorage.getItem('vehicle-search-filters')) } catch { return false }
  })
  const [filters, setFilters] = useState<VehicleFilterParams>(() => {
    try {
      const saved = sessionStorage.getItem('vehicle-search-filters')
      return saved ? { category: 'ALL', transmission: 'ALL', ...JSON.parse(saved) as VehicleFilterParams } : { category: 'ALL', transmission: 'ALL' }
    } catch {
      return { category: 'ALL', transmission: 'ALL' }
    }
  })
  const [location, setLocation] = useState(() => {
    try { return sessionStorage.getItem('vehicle-search-location') ?? 'Finding your location...' } catch { return 'Finding your location...' }
  })
  const [locationFromGoogle, setLocationFromGoogle] = useState(false)
  const [locating, setLocating] = useState(false)
  const [isExpanded, setIsExpanded] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedVehicleForBooking, setSelectedVehicleForBooking] = useState<Vehicle | null>(null)
  const [mapZoom, setMapZoom] = useState(1)
  const [mapOffset, setMapOffset] = useState({ x: 0, y: 0 })
  const [mapDrag, setMapDrag] = useState<{ pointerId: number; x: number; y: number; offsetX: number; offsetY: number } | null>(null)
  const { data: vehicles = [], isLoading, isError, refetch } = useVehicles(filters)

  const sortedVehicles = useMemo(() => vehicles, [vehicles])
  const selectedVehicle = sortedVehicles.find((vehicle) => vehicle.id === selectedId) ?? sortedVehicles[0]

  const locate = useCallback(() => {
    if (!navigator.geolocation) {
      setLocation('Ho Chi Minh City')
      setLocationFromGoogle(false)
      setFilters((current) => ({ ...current, location: undefined }))
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
  }, [])

  const changeLocation = (value: string) => {
    setLocation(value)
    setLocationFromGoogle(false)
    setFilters((current) => ({ ...current, location: value.trim() || undefined }))
  }

  useEffect(() => { if (!hasSavedFilters || location === 'Finding your location...') locate() }, [hasSavedFilters, location, locate])
  useEffect(() => {
    try { sessionStorage.setItem('vehicle-search-filters', JSON.stringify(filters)) } catch { /* Search still works without storage access. */ }
  }, [filters])
  useEffect(() => {
    try { sessionStorage.setItem('vehicle-search-location', location) } catch { /* Search still works without storage access. */ }
  }, [location])
  useEffect(() => {
    if (!selectedId && sortedVehicles.length) setSelectedId(sortedVehicles[0].id)
    else if (selectedId && !sortedVehicles.some((vehicle) => vehicle.id === selectedId)) setSelectedId(sortedVehicles[0]?.id ?? null)
  }, [sortedVehicles, selectedId])

  useEffect(() => {
    const state = routerLocation.state as { resumeBookingVehicleId?: string; resumeBookingVehicle?: Vehicle; pickupAt?: string; returnAt?: string } | null
    if (!state?.resumeBookingVehicleId) return
    const vehicle = sortedVehicles.find((item) => item.id === state.resumeBookingVehicleId)
      ?? (state.resumeBookingVehicle?.id === state.resumeBookingVehicleId ? state.resumeBookingVehicle : undefined)
    if (!vehicle) return
    setSelectedVehicleForBooking(vehicle)
    setFilters((current) => ({ ...current, pickupAt: state.pickupAt, returnAt: state.returnAt }))
    navigate(`${routerLocation.pathname}${routerLocation.search}`, {
      replace: true,
      state: { pickupAt: state.pickupAt, returnAt: state.returnAt },
    })
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
      <VehicleFilterBar location={location} locationFromGoogle={locationFromGoogle} onLocationChange={changeLocation} onLocate={locate} locating={locating} filters={filters} onApply={setFilters} />
      <div className="search-map-stage">
        <div className={`map-canvas ${mapDrag ? 'is-dragging' : ''}`} aria-label="Map of matching vehicles"
          onPointerDown={(event) => {
            if ((event.target as HTMLElement).closest('button')) return
            setMapDrag({ pointerId: event.pointerId, x: event.clientX, y: event.clientY, offsetX: mapOffset.x, offsetY: mapOffset.y })
            event.currentTarget.setPointerCapture(event.pointerId)
          }}
          onPointerMove={(event) => {
            if (!mapDrag || mapDrag.pointerId !== event.pointerId) return
            setMapOffset({ x: mapDrag.offsetX + event.clientX - mapDrag.x, y: mapDrag.offsetY + event.clientY - mapDrag.y })
          }}
          onPointerUp={() => setMapDrag(null)}
          onPointerCancel={() => setMapDrag(null)}>
          <div className="map-content" style={{ transform: `translate(${mapOffset.x}px, ${mapOffset.y}px) scale(${mapZoom})` }}>
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
          <div className="map-controls" aria-label="Map controls">
            <button type="button" aria-label="Zoom in" onClick={() => setMapZoom((zoom) => Math.min(zoom + 0.2, 2.2))}><Plus size={16} /></button>
            <button type="button" aria-label="Zoom out" onClick={() => setMapZoom((zoom) => Math.max(zoom - 0.2, 0.8))}><Minus size={16} /></button>
            <button type="button" aria-label="Reset map position" onClick={() => { setMapZoom(1); setMapOffset({ x: 0, y: 0 }) }}><RotateCcw size={14} /></button>
          </div>
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
              {(isExpanded ? sortedVehicles : selectedVehicle ? [selectedVehicle] : []).map((vehicle) => <VehicleCard key={vehicle.id} vehicle={vehicle} onBook={setSelectedVehicleForBooking} onViewDetails={(car) => navigate(`/vehicles/${car.id}`, { state: { pickupAt: filters.pickupAt, returnAt: filters.returnAt } })} />)}
            </div>
          )}
        </div>
      </section>
      <BookingModal vehicle={selectedVehicleForBooking} onClose={() => setSelectedVehicleForBooking(null)} onSuccess={(booking) => navigate(`/booking/${booking.id}`)} initialStartDate={filters.pickupAt} initialEndDate={filters.returnAt} />
    </div>
  )
}

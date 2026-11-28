import { useEffect, useMemo, useRef, useState } from 'react'
import {
  LngLatBounds,
  Map as MapLibreMap,
  Marker,
  setWorkerUrl,
} from 'maplibre-gl'
import maplibreWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'
import { MapPinned } from 'lucide-react'
import 'maplibre-gl/dist/maplibre-gl.css'
import type { Vehicle } from '@/types/vehicle.types'

const GOONG_STYLE_URL = 'https://tiles.goong.io/assets/goong_map_web.json'
const DEFAULT_CENTER: [number, number] = [106.7009, 10.7769]
const MAP_LOAD_TIMEOUT_MS = 15_000
const GOONG_MAPTILES_KEY = import.meta.env.VITE_GOONG_MAPTILES_KEY

setWorkerUrl(maplibreWorkerUrl)

type MapStatus = 'loading' | 'ready' | 'missing-key' | 'unsupported' | 'error'

interface GoongVehicleMapProps {
  vehicles: Vehicle[]
  selectedVehicleId?: string
  currentLocation?: { latitude: number; longitude: number }
  onSelect: (vehicle: Vehicle) => void
}

interface VehicleMarker {
  marker: Marker
  element: HTMLButtonElement
}

function withGoongKey(url: string, key: string) {
  if (!url.startsWith('https://tiles.goong.io/')) return url
  const requestUrl = new URL(url)
  requestUrl.searchParams.set('api_key', key)
  return requestUrl.toString()
}

function markerLabel(vehicle: Vehicle) {
  return new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 })
    .format(vehicle.pricePerDay)
}

function mapMessage(status: MapStatus) {
  if (status === 'missing-key') return 'Add a Goong Maptiles key to display the map.'
  if (status === 'unsupported') return 'This device cannot display the interactive map.'
  if (status === 'error') return 'The map could not load. Vehicle results are still available below.'
  return 'Loading the map…'
}

export function GoongVehicleMap({ vehicles, selectedVehicleId, currentLocation, onSelect }: GoongVehicleMapProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<MapLibreMap | null>(null)
  const markersRef = useRef(new Map<string, VehicleMarker>())
  const currentLocationMarkerRef = useRef<Marker | null>(null)
  const onSelectRef = useRef(onSelect)
  const locatedVehicles = useMemo(
    () => vehicles.filter((vehicle) => vehicle.latitude != null && vehicle.longitude != null),
    [vehicles],
  )
  const [initialPosition] = useState<{ center: [number, number]; zoom: number }>(() => {
    const firstVehicle = locatedVehicles[0]
    return {
      center: firstVehicle
        ? [firstVehicle.longitude!, firstVehicle.latitude!]
        : currentLocation
          ? [currentLocation.longitude, currentLocation.latitude]
          : DEFAULT_CENTER as [number, number],
      zoom: firstVehicle || currentLocation ? 12 : 5,
    }
  })
  const [status, setStatus] = useState<MapStatus>(GOONG_MAPTILES_KEY ? 'loading' : 'missing-key')

  useEffect(() => {
    onSelectRef.current = onSelect
  }, [onSelect])

  useEffect(() => {
    if (!containerRef.current) return
    if (!GOONG_MAPTILES_KEY) return

    let hasLoaded = false
    let map: MapLibreMap
    try {
      map = new MapLibreMap({
        container: containerRef.current,
        style: GOONG_STYLE_URL,
        center: initialPosition.center,
        zoom: initialPosition.zoom,
        minZoom: 4,
        maxZoom: 19,
        pitchWithRotate: false,
        dragRotate: false,
        touchPitch: false,
        attributionControl: false,
        transformRequest: (url) => ({ url: withGoongKey(url, GOONG_MAPTILES_KEY) }),
      })
    } catch {
      queueMicrotask(() => setStatus('unsupported'))
      return
    }
    mapRef.current = map
    const vehicleMarkers = markersRef.current

    const loadTimeout = window.setTimeout(() => {
      if (!hasLoaded) setStatus('error')
    }, MAP_LOAD_TIMEOUT_MS)
    map.once('load', () => {
      hasLoaded = true
      window.clearTimeout(loadTimeout)
      window.requestAnimationFrame(() => {
        map.resize()
        window.requestAnimationFrame(() => setStatus('ready'))
      })
    })
    map.on('error', (event) => {
      if (!hasLoaded) {
        console.error('Goong map failed to load.', event.error)
        setStatus('error')
      }
    })

    const resizeObserver = new ResizeObserver(() => map.resize())
    resizeObserver.observe(containerRef.current)

    return () => {
      window.clearTimeout(loadTimeout)
      resizeObserver.disconnect()
      vehicleMarkers.forEach(({ marker }) => marker.remove())
      vehicleMarkers.clear()
      currentLocationMarkerRef.current?.remove()
      currentLocationMarkerRef.current = null
      map.remove()
      mapRef.current = null
    }
  }, [initialPosition])

  useEffect(() => {
    const map = mapRef.current
    if (!map || status !== 'ready') return

    markersRef.current.forEach(({ marker }) => marker.remove())
    markersRef.current.clear()

    const bounds = new LngLatBounds()
    locatedVehicles.forEach((vehicle) => {
      const element = document.createElement('button')
      element.type = 'button'
      element.className = 'goong-vehicle-marker'
      element.setAttribute('aria-label', `Select ${vehicle.name}, ${markerLabel(vehicle)} VND per day`)
      const label = document.createElement('span')
      label.textContent = markerLabel(vehicle)
      element.append(label)
      element.addEventListener('click', () => onSelectRef.current(vehicle))

      const coordinates: [number, number] = [vehicle.longitude!, vehicle.latitude!]
      const marker = new Marker({ element, anchor: 'bottom' })
        .setLngLat(coordinates)
        .addTo(map)
      markersRef.current.set(vehicle.id, { marker, element })
      bounds.extend(coordinates)
    })

    if (locatedVehicles.length === 1) {
      map.easeTo({ center: bounds.getCenter(), zoom: 13, padding: { top: 134, right: 42, bottom: 150, left: 42 }, duration: 0 })
    } else if (locatedVehicles.length > 1) {
      map.fitBounds(bounds, { padding: { top: 134, right: 42, bottom: 150, left: 42 }, maxZoom: 14, duration: 0 })
    } else if (currentLocation) {
      map.easeTo({ center: [currentLocation.longitude, currentLocation.latitude], zoom: 12, padding: { top: 134, right: 42, bottom: 150, left: 42 }, duration: 0 })
    }
  }, [locatedVehicles, currentLocation, status])

  useEffect(() => {
    markersRef.current.forEach(({ element }, vehicleId) => {
      element.classList.toggle('is-active', vehicleId === selectedVehicleId)
    })
  }, [selectedVehicleId, status])

  useEffect(() => {
    const map = mapRef.current
    currentLocationMarkerRef.current?.remove()
    currentLocationMarkerRef.current = null
    if (!map || status !== 'ready' || !currentLocation) return

    const element = document.createElement('div')
    element.className = 'goong-current-location'
    element.setAttribute('aria-label', 'Your current location')
    currentLocationMarkerRef.current = new Marker({ element })
      .setLngLat([currentLocation.longitude, currentLocation.latitude])
      .addTo(map)
  }, [currentLocation, status])

  return (
    <div className="goong-map-shell" aria-label="Map of matching vehicles">
      <div ref={containerRef} className="goong-map" role="region" aria-label="Interactive Goong map" />
      <span className="goong-map-attribution" aria-label="Map data by Goong">© Goong</span>
      {status !== 'ready' && (
        <div className="goong-map-status" role={status === 'error' ? 'alert' : 'status'}>
          <MapPinned size={22} aria-hidden="true" />
          <span>{mapMessage(status)}</span>
        </div>
      )}
    </div>
  )
}

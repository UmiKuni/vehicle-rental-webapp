interface GoogleAddressComponent {
  long_name: string
  types: string[]
}

interface GoogleGeocodeResult {
  formatted_address: string
  address_components: GoogleAddressComponent[]
}

interface GoogleGeocoder {
  geocode(request: { location: { lat: number; lng: number } }): Promise<{ results: GoogleGeocodeResult[] }>
}

declare global {
  interface Window {
    google?: { maps?: { Geocoder?: new () => GoogleGeocoder } }
  }
}

let mapsScriptPromise: Promise<void> | undefined

function loadGoogleMaps(): Promise<void> {
  if (window.google?.maps?.Geocoder) return Promise.resolve()
  if (mapsScriptPromise) return mapsScriptPromise

  const key = import.meta.env.VITE_GOOGLE_MAPS_API_KEY
  if (!key) return Promise.reject(new Error('Google Maps API key is not configured.'))

  mapsScriptPromise = new Promise((resolve, reject) => {
    const script = document.createElement('script')
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}&v=weekly&language=vi&region=VN`
    script.async = true
    script.defer = true
    const timeout = window.setTimeout(() => reject(new Error('Google Maps load timed out.')), 15000)
    script.onload = () => {
      window.clearTimeout(timeout)
      window.google?.maps?.Geocoder
        ? resolve()
        : reject(new Error('Google Maps Geocoder did not load.'))
    }
    script.onerror = () => {
      window.clearTimeout(timeout)
      script.remove()
      reject(new Error('Google Maps could not be reached.'))
    }
    document.head.append(script)
  }).catch((error: unknown) => {
    mapsScriptPromise = undefined
    throw error
  })

  return mapsScriptPromise
}

function componentName(components: GoogleAddressComponent[], type: string) {
  return components.find((component) => component.types.includes(type))?.long_name
}

export async function reverseGeocode(latitude: number, longitude: number): Promise<string> {
  await loadGoogleMaps()
  const Geocoder = window.google?.maps?.Geocoder
  if (!Geocoder) throw new Error('Google Maps Geocoder is unavailable.')

  const { results } = await new Geocoder().geocode({ location: { lat: latitude, lng: longitude } })
  const components = results[0]?.address_components ?? []
  const district = componentName(components, 'sublocality_level_1')
    ?? componentName(components, 'neighborhood')
    ?? componentName(components, 'administrative_area_level_3')
  const city = componentName(components, 'locality')
    ?? componentName(components, 'administrative_area_level_1')
  const country = componentName(components, 'country')
  const address = [...new Set([district, city, country].filter(Boolean))].join(', ')

  if (!address) throw new Error('No readable area was returned for this location.')
  return address
}

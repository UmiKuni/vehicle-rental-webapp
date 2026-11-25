interface GoongAddressComponent {
  long_name: string
}

interface GoongCompound {
  commune?: string
  district?: string
  province?: string
}

interface GoongGeocodeResult {
  formatted_address?: string
  address_components?: GoongAddressComponent[]
  compound?: GoongCompound
}

interface GoongGeocodeResponse {
  results?: GoongGeocodeResult[]
  status?: string
  error_message?: string
}

const GOONG_GEOCODE_URL = 'https://rsapi.goong.io/v2/geocode'
const REQUEST_TIMEOUT_MS = 15_000

function uniqueArea(parts: Array<string | undefined>) {
  return [...new Set(parts.map((part) => part?.trim()).filter(Boolean))].join(', ')
}

function readableArea(result: GoongGeocodeResult | undefined) {
  if (!result) return ''

  const areaFromCompound = uniqueArea([
    result.compound?.commune,
    result.compound?.district,
    result.compound?.province,
  ])
  if (areaFromCompound) return areaFromCompound

  const areaFromComponents = uniqueArea(
    result.address_components?.slice(-3).map((component) => component.long_name) ?? [],
  )
  return areaFromComponents || result.formatted_address?.trim() || ''
}

export async function reverseGeocode(latitude: number, longitude: number): Promise<string> {
  const key = import.meta.env.VITE_GOONG_API_KEY
  if (!key) throw new Error('Goong API key is not configured.')

  const query = new URLSearchParams({
    latlng: `${latitude},${longitude}`,
    limit: '1',
    api_key: key,
  })
  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS)

  try {
    const response = await fetch(`${GOONG_GEOCODE_URL}?${query}`, { signal: controller.signal })
    if (!response.ok) throw new Error(`Goong request failed with status ${response.status}.`)

    const data = await response.json() as GoongGeocodeResponse
    if (data.status && data.status !== 'OK') {
      throw new Error(data.error_message || `Goong returned ${data.status}.`)
    }

    const address = readableArea(data.results?.[0])
    if (!address) throw new Error('No readable area was returned for this location.')
    return address
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new Error('Goong location lookup timed out.', { cause: error })
    }
    throw error
  } finally {
    window.clearTimeout(timeout)
  }
}

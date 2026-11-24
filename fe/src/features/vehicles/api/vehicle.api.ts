import apiClient from '@/shared/lib/axios'
import type { Vehicle, VehicleFilterParams } from '@/types/vehicle.types'

interface VehicleSearchItem {
  vehicleId: string
  brand: string
  model: string
  seats: number
  transmission: string
  address: string
  location: { latitude: number; longitude: number }
  priceFrom: number
  averageRating: number
  totalReviews: number
}

interface VehicleSearchPage {
  items: VehicleSearchItem[]
  totalPages: number
}

interface FleetVehicle {
  vehicleId: string
  status: string
  brandName: string
  modelName: string
  plateNumber: string
  transmission: string
  seats: number
  primaryImageKey?: string | null
  address: string
  location: { latitude: number; longitude: number }
}

interface Money { amount: number }
interface PricingPlan {
  weekdayRate: Money
  weekendRate: Money
  depositAmount: Money
}

async function searchAllPages(params: Record<string, string | number | undefined>): Promise<VehicleSearchItem[]> {
  const firstPage = await apiClient.get<VehicleSearchPage>('/search/vehicles', { params: { ...params, page: 1 } })
  const items = [...firstPage.data.items]
  for (let page = 2; page <= firstPage.data.totalPages; page += 1) {
    const { data } = await apiClient.get<VehicleSearchPage>('/search/vehicles', { params: { ...params, page } })
    items.push(...data.items)
  }
  return items
}

function mapVehicle(
  vehicle: Pick<FleetVehicle, 'vehicleId' | 'status' | 'brandName' | 'modelName' | 'transmission' | 'seats' | 'address' | 'primaryImageKey' | 'location'>,
  pricePerDay = 0,
  rating = 0,
  reviewCount = 0,
  depositAmount = 0,
): Vehicle {
  return {
    id: vehicle.vehicleId,
    name: `${vehicle.brandName} ${vehicle.modelName}`,
    brand: vehicle.brandName,
    modelYear: undefined,
    transmission: vehicle.transmission.toLowerCase().includes('manual') ? 'MANUAL' : 'AUTOMATIC',
    seats: vehicle.seats,
    pricePerDay,
    imageUrl: vehicle.primaryImageKey?.startsWith('http') ? vehicle.primaryImageKey : undefined,
    rating,
    location: vehicle.address,
    latitude: vehicle.location.latitude,
    longitude: vehicle.location.longitude,
    features: [],
    isAvailable: vehicle.status.toLowerCase() === 'published',
    depositAmount,
    reviewCount,
  }
}

export const vehicleApi = {
  /** Search published vehicles through the real Search module API. */
  getVehicles: async (params?: VehicleFilterParams): Promise<Vehicle[]> => {
    const commonParams = {
        seats: params?.seats,
        transmission: params?.transmission === 'ALL' ? undefined
          : params?.transmission === 'AUTOMATIC' ? 'Auto'
            : params?.transmission === 'MANUAL' ? 'Manual' : undefined,
        minPrice: params?.minPrice,
        maxPrice: params?.maxPrice,
        latitude: params?.latitude,
        longitude: params?.longitude,
        radiusKm: params?.latitude != null && params?.longitude != null ? 25 : undefined,
        pageSize: 100,
    }
    const search = params?.search?.trim()
    const requests = params?.brand
      ? [searchAllPages({ ...commonParams, brand: params.brand, model: search || undefined })]
      : search
        ? [
            searchAllPages({ ...commonParams, brand: search }),
            searchAllPages({ ...commonParams, model: search }),
          ]
        : [searchAllPages(commonParams)]
    const pages = await Promise.all(requests)
    const results = new Map<string, VehicleSearchItem>()
    pages.forEach((items) => items.forEach((vehicle) => results.set(vehicle.vehicleId, vehicle)))

    return [...results.values()].map((vehicle) => mapVehicle({
      vehicleId: vehicle.vehicleId,
      status: 'Published',
      brandName: vehicle.brand,
      modelName: vehicle.model,
      transmission: vehicle.transmission,
      seats: vehicle.seats,
      address: vehicle.address,
      location: vehicle.location,
    }, vehicle.priceFrom, vehicle.averageRating, vehicle.totalReviews))
  },

  /** Load the fleet summary and pricing plan for one vehicle. */
  getVehicleById: async (id: string): Promise<Vehicle> => {
    const { data: vehicle } = await apiClient.get<FleetVehicle>(`/fleet/vehicles/${id}`)
    const { data: pricing } = await apiClient.get<PricingPlan>(`/pricing/vehicles/${id}`)
    return mapVehicle(
      vehicle,
      Math.min(pricing.weekdayRate.amount, pricing.weekendRate.amount),
      0,
      0,
      pricing.depositAmount.amount,
    )
  },
}

export const availabilityApi = {
  check: async (vehicleId: string, start: string, end: string): Promise<{ isAvailable: boolean; conflictReason?: string | null }> => {
    const { data } = await apiClient.get<{ isAvailable: boolean; conflictReason?: string | null }>(
      `/availability/vehicles/${vehicleId}/check`,
      { params: { start: new Date(`${start}T00:00:00`).toISOString(), end: new Date(`${end}T00:00:00`).toISOString() } },
    )
    return data
  },
}

export const pricingApi = {
  quote: async (vehicleId: string, start: string, end: string): Promise<{ total: Money; depositAmount: Money }> => {
    const { data } = await apiClient.post<{ total: Money; depositAmount: Money }>('/pricing/quotes', {
      vehicleId,
      startTime: new Date(`${start}T00:00:00`).toISOString(),
      endTime: new Date(`${end}T00:00:00`).toISOString(),
    })
    return data
  },
}

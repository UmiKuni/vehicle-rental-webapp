import apiClient from '@/shared/lib/axios'
import type { Vehicle, VehicleFilterParams } from '@/types/vehicle.types'

interface VehicleSearchItem {
  vehicleId: string
  brand: string
  model: string
  seats: number
  transmission: string
  address: string
  priceFrom: number
  averageRating: number
  totalReviews: number
}

interface VehicleSearchPage {
  items: VehicleSearchItem[]
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
}

interface Money { amount: number }
interface PricingPlan {
  weekdayRate: Money
  weekendRate: Money
  depositAmount: Money
}

function mapVehicle(
  vehicle: Pick<FleetVehicle, 'vehicleId' | 'status' | 'brandName' | 'modelName' | 'transmission' | 'seats' | 'address' | 'primaryImageKey'>,
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
    category: vehicle.seats >= 6 ? 'CAR_7_SEAT' : 'CAR_4_SEAT',
    transmission: vehicle.transmission.toLowerCase().includes('manual') ? 'MANUAL' : 'AUTOMATIC',
    fuelType: 'GASOLINE',
    seats: vehicle.seats,
    pricePerDay,
    imageUrl: vehicle.primaryImageKey?.startsWith('http')
      ? vehicle.primaryImageKey
      : 'https://placehold.co/800x500?text=Vehicle',
    rating,
    tripsCount: reviewCount,
    location: vehicle.address,
    features: [],
    isAvailable: vehicle.status.toLowerCase() === 'published',
    depositAmount,
    reviewCount,
  }
}

export const vehicleApi = {
  /** Search published vehicles through the real Search module API. */
  getVehicles: async (params?: VehicleFilterParams): Promise<Vehicle[]> => {
    const { data } = await apiClient.get<VehicleSearchPage>('/search/vehicles', {
      params: {
        brand: params?.brand ?? params?.search,
        model: params?.brand ? params?.search : undefined,
        seats: params?.seats,
        transmission: params?.transmission === 'ALL' ? undefined
          : params?.transmission === 'AUTOMATIC' ? 'Auto'
            : params?.transmission === 'MANUAL' ? 'Manual' : undefined,
        minPrice: params?.minPrice,
        maxPrice: params?.maxPrice,
        page: 1,
        pageSize: 100,
      },
    })

    return data.items.map((vehicle) => mapVehicle({
      vehicleId: vehicle.vehicleId,
      status: 'Published',
      brandName: vehicle.brand,
      modelName: vehicle.model,
      transmission: vehicle.transmission,
      seats: vehicle.seats,
      address: vehicle.address,
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

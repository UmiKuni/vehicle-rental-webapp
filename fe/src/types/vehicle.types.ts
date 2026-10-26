import type { VehicleInterest } from './kyc.types'

export type TransmissionType = 'AUTOMATIC' | 'MANUAL'
export type FuelType = 'GASOLINE' | 'DIESEL' | 'ELECTRIC' | 'HYBRID'

export interface Vehicle {
  id: string
  name: string
  brand: string
  modelYear: number
  category: VehicleInterest
  transmission: TransmissionType
  fuelType: FuelType
  seats: number
  pricePerDay: number
  imageUrl: string
  rating: number
  tripsCount: number
  location: string
  features: string[]
  isAvailable: boolean
  depositAmount: number
}

export interface VehicleFilterParams {
  category?: VehicleInterest | 'ALL'
  search?: string
  minPrice?: number
  maxPrice?: number
  seats?: number
  transmission?: TransmissionType | 'ALL'
  location?: string
  brand?: string
  pickupAt?: string
  returnAt?: string
}

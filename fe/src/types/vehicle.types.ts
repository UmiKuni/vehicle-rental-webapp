import type { VehicleInterest } from './kyc.types'

export type TransmissionType = 'AUTOMATIC' | 'MANUAL'
export type FuelType = 'GASOLINE' | 'DIESEL' | 'ELECTRIC' | 'HYBRID'

export interface VehicleProvider {
  name: string
  avatarUrl?: string
  rating?: number
  reviewCount?: number
  description?: string
}

export interface VehicleFee {
  name: string
  amount: number
  description?: string
}

export interface VehicleReview {
  id: string
  reviewerName: string
  rating: number
  comment: string
  createdAt: string
}

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
  reviewCount?: number
  /** Optional fields returned by expanded vehicle detail APIs. */
  images?: string[]
  description?: string
  provider?: VehicleProvider | null
  additionalFees?: VehicleFee[]
  deliveryFeeAmount?: number
  reviews?: VehicleReview[]
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

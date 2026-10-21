import type { Vehicle } from './vehicle.types'

export type BookingStatus = 'CONFIRMED' | 'ACTIVE' | 'COMPLETED' | 'CANCELLED'
export type DeliveryType = 'STATION' | 'DOORSTEP'

export interface Booking {
  id: string
  bookingCode: string
  vehicleId: string
  vehicle: Vehicle
  userId: number
  customerName: string
  customerPhone: string
  startDate: string
  endDate: string
  totalDays: number
  pricePerDay: number
  totalAmount: number
  depositAmount: number
  deliveryType: DeliveryType
  deliveryAddress?: string
  status: BookingStatus
  createdAt: string
  note?: string
}

export interface CreateBookingPayload {
  vehicleId: string
  startDate: string
  endDate: string
  deliveryType: DeliveryType
  deliveryAddress?: string
  customerName: string
  customerPhone: string
  note?: string
}

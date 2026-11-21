export type BookingStatus = 'PENDING' | 'CONFIRMED' | 'PAID' | 'ACTIVE' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED' | 'REJECTED' | 'EXPIRED'

export interface BookedVehicle {
  id: string
  name: string
  imageUrl?: string
  pricePerDay: number
}

export interface Booking {
  id: string
  bookingCode: string
  vehicleId: string
  vehicle: BookedVehicle
  userId: number | string
  customerName: string
  customerPhone: string
  startDate: string
  endDate: string
  totalDays: number
  pricePerDay: number
  totalAmount: number
  depositAmount: number
  pickupLocation: string
  status: BookingStatus
  createdAt: string
  note?: string
}

export interface CreateBookingPayload {
  vehicleId: string
  startDate: string
  endDate: string
  pickupLocation: string
  note?: string
}

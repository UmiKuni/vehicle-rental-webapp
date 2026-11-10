import apiClient from '@/shared/lib/axios'
import type { Vehicle } from '@/types/vehicle.types'
import type { Booking, BookingStatus, CreateBookingPayload } from '@/types/booking.types'

interface BookingSummaryDto {
  id: string
  vehicleId: string
  vehicleModel: string
  vehiclePlate: string
  startTime: string
  endTime: string
  status: string
  totalAmount: number
  createdAt: string
}

interface BookingDetailDto extends BookingSummaryDto {
  customerId: string
  customerName: string
  customerPhone: string
  ownerId: string
  pickupLocation: string
  totalRentAmount: number
  securityDepositAmount: number
}

interface PagedBookings { items: BookingSummaryDto[] }

function mapStatus(status: string): BookingStatus {
  const normalized = status.replace(/([a-z])([A-Z])/g, '$1_$2').toUpperCase()
  return normalized as BookingStatus
}

function mapBooking(dto: BookingSummaryDto | BookingDetailDto): Booking {
  const start = new Date(dto.startTime)
  const end = new Date(dto.endTime)
  const totalDays = Math.max(1, Math.ceil((end.getTime() - start.getTime()) / 86_400_000))
  const detail = dto as BookingDetailDto
  const vehicle: Vehicle = {
    id: dto.vehicleId,
    name: dto.vehicleModel,
    brand: dto.vehicleModel.split(' ')[0] ?? 'Vehicle',
    category: 'CAR_4_SEAT',
    transmission: 'AUTOMATIC',
    fuelType: 'GASOLINE',
    seats: 0,
    pricePerDay: (detail.totalRentAmount ?? dto.totalAmount) / totalDays,
    imageUrl: 'https://placehold.co/800x500?text=Vehicle',
    rating: 0,
    tripsCount: 0,
    location: detail.pickupLocation ?? '',
    features: [],
    isAvailable: true,
    depositAmount: detail.securityDepositAmount ?? 0,
  }

  return {
    id: dto.id,
    bookingCode: dto.id.slice(0, 8).toUpperCase(),
    vehicleId: dto.vehicleId,
    vehicle,
    userId: detail.customerId ?? '',
    customerName: detail.customerName ?? '',
    customerPhone: detail.customerPhone ?? '',
    startDate: dto.startTime,
    endDate: dto.endTime,
    totalDays,
    pricePerDay: vehicle.pricePerDay,
    totalAmount: dto.totalAmount,
    depositAmount: detail.securityDepositAmount ?? 0,
    deliveryType: 'STATION',
    deliveryAddress: detail.pickupLocation,
    status: mapStatus(dto.status),
    createdAt: dto.createdAt,
  }
}

export const bookingApi = {
  getMyBookings: async (): Promise<Booking[]> => {
    const { data } = await apiClient.get<PagedBookings>('/bookings/me')
    return data.items.map(mapBooking)
  },

  createBooking: async (payload: CreateBookingPayload): Promise<Booking> => {
    const { data } = await apiClient.post<BookingDetailDto>('/bookings/', {
      vehicleId: payload.vehicleId,
      startTime: new Date(`${payload.startDate.slice(0, 10)}T00:00:00Z`).toISOString(),
      endTime: new Date(`${payload.endDate.slice(0, 10)}T00:00:00Z`).toISOString(),
      pickupLocation: payload.deliveryType === 'DOORSTEP' ? payload.deliveryAddress : 'Vehicle rental station',
      includeInsurance: false,
      notes: payload.note ?? null,
    })
    return mapBooking(data)
  },

  cancelBooking: async (id: string): Promise<void> => {
    await apiClient.put(`/bookings/${id}/cancel`, { reason: 'Cancelled by customer' })
  },
}

import apiClient from '@/shared/lib/axios'
import type { Booking, BookingStatus, CreateBookingPayload } from '@/types/booking.types'

interface BookingSummaryDto {
  id: string
  vehicleId: string
  vehicleModel: string
  vehiclePlate: string
  startTime: string
  endTime: string
  status: string
  totalAmount?: number
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

interface PagedBookings {
  items: BookingSummaryDto[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface BookingCancellationResult {
  status: string
  penaltyAmount: number
  refundAmount: number
}

async function getAllMyBookings(): Promise<BookingSummaryDto[]> {
  const pageSize = 100
  const firstPage = await apiClient.get<PagedBookings>('/bookings/me', { params: { page: 1, pageSize } })
  const items = [...firstPage.data.items]
  for (let page = 2; page <= firstPage.data.totalPages; page += 1) {
    const { data } = await apiClient.get<PagedBookings>('/bookings/me', { params: { page, pageSize } })
    items.push(...data.items)
  }
  return items
}

function mapStatus(status: string): BookingStatus {
  const normalized = status.replace(/([a-z])([A-Z])/g, '$1_$2').toUpperCase()
  return normalized as BookingStatus
}

function mapBooking(dto: BookingSummaryDto | BookingDetailDto): Booking {
  const start = new Date(dto.startTime)
  const end = new Date(dto.endTime)
  const totalDays = Math.max(1, Math.ceil((end.getTime() - start.getTime()) / 86_400_000))
  const detail = dto as BookingDetailDto
  const vehicle = {
    id: dto.vehicleId,
    name: dto.vehicleModel,
    pricePerDay: (detail.totalRentAmount ?? dto.totalAmount ?? 0) / totalDays,
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
    totalAmount: detail.totalRentAmount ?? dto.totalAmount ?? 0,
    depositAmount: detail.securityDepositAmount ?? 0,
    pickupLocation: detail.pickupLocation ?? '',
    status: mapStatus(dto.status),
    createdAt: dto.createdAt,
  }
}

export const bookingApi = {
  getMyBookings: async (): Promise<Booking[]> => {
    return (await getAllMyBookings()).map(mapBooking)
  },

  getBooking: async (id: string): Promise<Booking> => {
    const { data } = await apiClient.get<BookingDetailDto>(`/bookings/${id}`)
    return mapBooking(data)
  },

  createBooking: async (payload: CreateBookingPayload): Promise<Booking> => {
    const { data } = await apiClient.post<BookingDetailDto>('/bookings/', {
      vehicleId: payload.vehicleId,
      startTime: new Date(`${payload.startDate.slice(0, 10)}T00:00:00`).toISOString(),
      endTime: new Date(`${payload.endDate.slice(0, 10)}T00:00:00`).toISOString(),
      pickupLocation: payload.pickupLocation.trim(),
      includeInsurance: false,
      notes: payload.note ?? null,
    })
    return mapBooking(data)
  },

  cancelBooking: async (id: string): Promise<BookingCancellationResult> => {
    const { data } = await apiClient.put<BookingCancellationResult>(`/bookings/${id}/cancel`, { reason: 'Cancelled by customer' })
    return data
  },
}

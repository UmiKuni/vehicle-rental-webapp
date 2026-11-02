import apiClient from '@/shared/lib/axios'
import type { ApiResponse } from '@/types/api.types'
import type { Booking, CreateBookingPayload } from '@/types/booking.types'

export const bookingApi = {
  /** Get all bookings for current user */
  getMyBookings: async (): Promise<Booking[]> => {
    const res = await apiClient.get<ApiResponse<Booking[]>>('/bookings')
    return res.data.data
  },

  /** Create a new booking */
  createBooking: async (payload: CreateBookingPayload): Promise<Booking> => {
    const res = await apiClient.post<ApiResponse<Booking>>('/bookings', payload)
    return res.data.data
  },

  /** Cancel a booking */
  cancelBooking: async (id: string): Promise<Booking> => {
    const res = await apiClient.put<ApiResponse<Booking>>(`/bookings/${id}/cancel`)
    return res.data.data
  },
}

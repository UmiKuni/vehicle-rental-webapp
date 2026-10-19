import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { bookingApi } from '../api/booking.api'
import { QUERY_KEYS } from '@/shared/constants'
import { getErrorMessage } from '@/shared/lib/query-client'
import type { CreateBookingPayload } from '@/types/booking.types'

export function useBookings() {
  return useQuery({
    queryKey: [QUERY_KEYS.BOOKINGS],
    queryFn: bookingApi.getMyBookings,
    staleTime: 5 * 60 * 1000,
  })
}

export function useCreateBooking() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload: CreateBookingPayload) => bookingApi.createBooking(payload),
    onSuccess: (booking) => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEYS.BOOKINGS] })
      toast.success(`Đặt xe ${booking.vehicle.name} thành công! Mã đơn: ${booking.bookingCode}`)
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })
}

export function useCancelBooking() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => bookingApi.cancelBooking(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEYS.BOOKINGS] })
      toast.success('Đã hủy đơn đặt xe')
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })
}

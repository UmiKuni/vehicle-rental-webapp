import apiClient from '@/shared/lib/axios'
import type { VehicleReview } from '@/types/vehicle.types'

interface ReviewDto {
  reviewId: string
  rating: number
  content: string
  createdAt: string
}

export interface PagedReviews {
  items: ReviewDto[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface VehicleRatingSummary {
  vehicleId: string
  averageRating: number
  totalReviews: number
}

export const reviewApi = {
  getVehicleRatingSummary: async (vehicleId: string) => {
    const { data } = await apiClient.get<VehicleRatingSummary>(`/reviews/vehicles/${vehicleId}/summary`)
    return data
  },

  getVehicleReviews: async (vehicleId: string, page: number, pageSize = 5) => {
    const { data } = await apiClient.get<PagedReviews>(`/reviews/vehicles/${vehicleId}`, {
      params: { page, pageSize },
    })
    return {
      ...data,
      items: data.items.map((review): VehicleReview => ({
        id: review.reviewId,
        reviewerName: 'Verified renter',
        rating: review.rating,
        comment: review.content,
        createdAt: review.createdAt,
      })),
    }
  },

  createForBooking: async (bookingId: string, rating: number, content: string) => {
    const { data } = await apiClient.post(`/reviews/bookings/${bookingId}`, {
      rating,
      content: content.trim(),
    })
    return data
  },
}

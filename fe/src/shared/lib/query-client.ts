import { QueryClient } from '@tanstack/react-query'
import type { AxiosError } from 'axios'
import type { ApiError } from '@/types/api.types'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000, // 5 minutes
      retry: (failureCount, error) => {
        const axiosError = error as AxiosError<ApiError>
        // Don't retry on 4xx client errors
        if (axiosError.response?.status && axiosError.response.status < 500) {
          return false
        }
        return failureCount < 2
      },
    },
    mutations: {
      retry: false,
    },
  },
})

/** Extract a user-friendly error message from an API error */
export function getErrorMessage(error: unknown): string {
  const axiosError = error as AxiosError<ApiError>
  return (
    axiosError.response?.data?.message ??
    axiosError.message ??
    'Đã xảy ra lỗi. Vui lòng thử lại.'
  )
}

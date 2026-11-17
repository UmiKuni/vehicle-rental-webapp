import apiClient from '@/shared/lib/axios'

export interface PaymentCheckout {
  paymentId: string
  bookingId: string
  amount: number
  method: string
  paymentUrl: string
  qrCodeUrl?: string
  expiresAt: string
}

export interface PaymentDetail {
  id: string
  bookingId: string
  amount: number
  status: string
  method: string
  gatewayTransactionId?: string
  createdAt: string
  processedAt?: string | null
}

export interface DepositDetail {
  id: string
  bookingId: string
  amount: number
  status: string
  deductedAmount: number
  refundedAmount: number
  notes?: string | null
  updatedAt: string
}

export interface BookingPaymentSummary {
  bookingId: string
  mainPayment?: PaymentDetail | null
  deposit?: DepositDetail | null
}

export const paymentApi = {
  createCheckout: async (bookingId: string, method: string, returnUrl: string) => {
    const { data } = await apiClient.post<PaymentCheckout>('/payments/checkout', { bookingId, method, returnUrl })
    return data
  },
  getBookingPayment: async (bookingId: string) => {
    const { data } = await apiClient.get<BookingPaymentSummary>(`/payments/booking/${bookingId}`)
    return data
  },
  getPayment: async (paymentId: string) => {
    const { data } = await apiClient.get<PaymentDetail>(`/payments/${paymentId}`)
    return data
  },
}

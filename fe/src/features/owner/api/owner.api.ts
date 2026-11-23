import apiClient from '@/shared/lib/axios'

export interface OwnerBooking {
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

export interface OwnerVehicle {
  vehicleId: string
  ownerId: string
  status: string
  brandId: string
  brandName: string
  modelId: string
  modelName: string
  plateNumber: string
  transmission: string
  seats: number
  primaryImageKey?: string | null
  address: string
}

export interface OwnerPage<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface OwnerBookingDetail extends OwnerBooking {
  customerId: string
  customerName: string
  customerPhone: string
  ownerId: string
  pickupLocation: string
  totalRentAmount: number
  securityDepositAmount: number
}

export interface OwnerRevenueReport {
  ownerId: string
  month: number
  year: number
  totalTrips: number
  grossRevenue: number
  platformCommissionTotal: number
  netEarnings: number
  trips: Array<{
    bookingId: string
    vehiclePlate: string
    vehicleModel: string
    startDate: string
    endDate: string
    grossRent: number
    platformCommission: number
    netIncome: number
  }>
}

export interface VehicleBrand { brandId: string; name: string }
export interface VehicleModel { modelId: string; brandId: string; name: string }
export interface VehicleCalendarEntry {
  id: string
  vehicleId: string
  type: string
  startTime: string
  endTime: string
  expiresAt?: string | null
  bookingId?: string | null
  note?: string | null
}

export const ownerApi = {
  getBookings: async (page: number, pageSize: number, status?: string) =>
    (await apiClient.get<OwnerPage<OwnerBooking>>('/bookings/owner', {
      params: { page, pageSize, status: status || undefined },
    })).data,

  getBooking: async (bookingId: string) =>
    (await apiClient.get<OwnerBookingDetail>(`/bookings/${encodeURIComponent(bookingId)}`)).data,

  getVehicles: async (ownerId: string) =>
    (await apiClient.get<OwnerVehicle[]>(`/fleet/owners/${encodeURIComponent(ownerId)}/vehicles`)).data,

  getRevenue: async (month: number, year: number) =>
    (await apiClient.get<OwnerRevenueReport>('/reporting/owner/revenue', { params: { month, year } })).data,

  approveBooking: async (bookingId: string) =>
    apiClient.put(`/bookings/${encodeURIComponent(bookingId)}/approve`),

  rejectBooking: async (bookingId: string, reason: string) =>
    apiClient.put(`/bookings/${encodeURIComponent(bookingId)}/reject`, { reason }),

  getBrands: async () => (await apiClient.get<VehicleBrand[]>('/fleet/brands')).data,

  getModels: async (brandId?: string) =>
    (await apiClient.get<VehicleModel[]>('/fleet/models', { params: { brandId: brandId || undefined } })).data,

  createVehicle: async (payload: {
    ownerId: string
    plateNumber: string
    brandId: string
    modelId: string
    transmission: 'Auto' | 'Manual'
    seats: number
    latitude: number
    longitude: number
    address: string
  }) => (await apiClient.post<OwnerVehicle>('/fleet/vehicles', payload)).data,

  configurePricing: async (vehicleId: string, ownerId: string, prices: { weekday: number; weekend: number; deposit: number; kmLimit: number }) =>
    apiClient.put(`/pricing/vehicles/${encodeURIComponent(vehicleId)}`, {
      hourlyRate: null,
      weekdayRate: { amount: prices.weekday, currency: 'VND' },
      weekendRate: { amount: prices.weekend, currency: 'VND' },
      depositAmount: { amount: prices.deposit, currency: 'VND' },
      kmLimit: prices.kmLimit,
      overKmFee: null,
      cleaningFee: null,
      ownerId,
    }),

  submitVehicle: async (vehicleId: string) =>
    apiClient.post(`/fleet/vehicles/${encodeURIComponent(vehicleId)}/submit`),

  uploadVehicleImage: async (vehicleId: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return apiClient.post(`/fleet/vehicles/${encodeURIComponent(vehicleId)}/images`, form)
  },

  setVehicleVisibility: async (vehicleId: string, visible: boolean) =>
    apiClient.post(`/fleet/vehicles/${encodeURIComponent(vehicleId)}/${visible ? 'show' : 'hide'}`),

  getCalendar: async (vehicleId: string, from: string, to: string) =>
    (await apiClient.get<VehicleCalendarEntry[]>(`/availability/vehicles/${encodeURIComponent(vehicleId)}/calendar`, { params: { from, to } })).data,

  blockSchedule: async (vehicleId: string, request: { startTime: string; endTime: string; reason: string; type: 'OwnerBlocked' | 'Maintenance' }) =>
    (await apiClient.post<VehicleCalendarEntry>(`/availability/vehicles/${encodeURIComponent(vehicleId)}/blocks`, request)).data,

  unblockSchedule: async (blockId: string) =>
    apiClient.delete(`/availability/blocks/${encodeURIComponent(blockId)}`),
}

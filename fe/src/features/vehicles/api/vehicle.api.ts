import apiClient from '@/shared/lib/axios'
import type { ApiResponse } from '@/types/api.types'
import type { Vehicle, VehicleFilterParams } from '@/types/vehicle.types'

export const vehicleApi = {
  /** Get vehicles with optional filter criteria */
  getVehicles: async (params?: VehicleFilterParams): Promise<Vehicle[]> => {
    const res = await apiClient.get<ApiResponse<Vehicle[]>>('/vehicles', { params })
    return res.data.data
  },

  /** Get vehicle by ID */
  getVehicleById: async (id: string): Promise<Vehicle> => {
    const res = await apiClient.get<ApiResponse<Vehicle>>(`/vehicles/${id}`)
    return res.data.data
  },
}

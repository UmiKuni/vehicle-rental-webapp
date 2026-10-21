import { useQuery } from '@tanstack/react-query'
import { vehicleApi } from '../api/vehicle.api'
import { QUERY_KEYS } from '@/shared/constants'
import type { VehicleFilterParams } from '@/types/vehicle.types'

export function useVehicles(filters?: VehicleFilterParams) {
  return useQuery({
    queryKey: [QUERY_KEYS.VEHICLES, filters],
    queryFn: () => vehicleApi.getVehicles(filters),
    staleTime: 5 * 60 * 1000,
  })
}

export function useVehicleDetail(id?: string) {
  return useQuery({
    queryKey: [QUERY_KEYS.VEHICLE, id],
    queryFn: () => vehicleApi.getVehicleById(id!),
    enabled: !!id,
    staleTime: 5 * 60 * 1000,
  })
}

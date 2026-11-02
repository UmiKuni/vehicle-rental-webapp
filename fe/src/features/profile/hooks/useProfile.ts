import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { profileApi } from '../api/profile.api'
import { QUERY_KEYS } from '@/shared/constants'
import type { KycStatus } from '@/types/kyc.types'

export function useProfile() {
  return useQuery({
    queryKey: [QUERY_KEYS.PROFILE],
    queryFn: profileApi.getProfile,
    staleTime: 5 * 60 * 1000,
  })
}

export function useKycStatus(enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEYS.KYC_STATUS],
    queryFn: profileApi.getKycStatus,
    staleTime: 2 * 60 * 1000,
    enabled,
  })
}

export function useSetMockKycStatus() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (status: KycStatus) => profileApi.setMockKycStatus(status),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEYS.KYC_STATUS] })
      queryClient.invalidateQueries({ queryKey: [QUERY_KEYS.PROFILE] })
    },
  })
}

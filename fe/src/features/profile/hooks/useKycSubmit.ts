import { useMutation, useQueryClient } from '@tanstack/react-query'
import { profileApi } from '../api/profile.api'
import { QUERY_KEYS } from '@/shared/constants'
import type { KycSubmitPayload } from '@/types/kyc.types'
import { toast } from 'sonner'
import { getErrorMessage } from '@/shared/lib/query-client'

export function useKycSubmit() {
  const qc = useQueryClient()

  return useMutation({
    mutationFn: (payload: KycSubmitPayload) => profileApi.submitKyc(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [QUERY_KEYS.KYC_STATUS] })
      qc.invalidateQueries({ queryKey: [QUERY_KEYS.PROFILE] })
      toast.success('The backend mock verification accepted your ID and driver’s license details.')
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })
}

export function useKycImageUpload() {
  return useMutation({
    mutationFn: ({ file, side }: { file: File; side: 'front' | 'back' }) =>
      profileApi.uploadCccdImage(file, side),
  })
}

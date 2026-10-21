import { useMutation, useQueryClient } from '@tanstack/react-query'
import { profileApi } from '../api/profile.api'
import { QUERY_KEYS } from '@/shared/constants'
import type { KycSubmitPayload } from '@/types/kyc.types'
import { toast } from 'sonner'

export function useKycSubmit() {
  const qc = useQueryClient()

  return useMutation({
    mutationFn: (payload: KycSubmitPayload) => profileApi.submitKyc(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [QUERY_KEYS.KYC_STATUS] })
      qc.invalidateQueries({ queryKey: [QUERY_KEYS.PROFILE] })
      toast.success('Verification profile submitted successfully! We will review it within 1-2 business days.')
    },
    onError: () => {
      toast.error('Failed to submit verification profile. Please try again.')
    },
  })
}

export function useKycImageUpload() {
  return useMutation({
    mutationFn: ({ file, side }: { file: File; side: 'front' | 'back' }) =>
      profileApi.uploadCccdImage(file, side),
  })
}

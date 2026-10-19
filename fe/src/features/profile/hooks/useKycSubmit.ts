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
      toast.success('Hồ sơ xác thực đã được gửi thành công! Chúng tôi sẽ xem xét trong 1-2 ngày làm việc.')
    },
    onError: () => {
      toast.error('Gửi hồ sơ thất bại. Vui lòng thử lại.')
    },
  })
}

export function useKycImageUpload() {
  return useMutation({
    mutationFn: ({ file, side }: { file: File; side: 'front' | 'back' }) =>
      profileApi.uploadCccdImage(file, side),
  })
}

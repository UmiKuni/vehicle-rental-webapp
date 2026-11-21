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
      toast.success('Documents uploaded and backend verification checks completed.')
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })
}

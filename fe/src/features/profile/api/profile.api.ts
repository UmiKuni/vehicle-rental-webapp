import apiClient from '@/shared/lib/axios'
import type { ApiResponse } from '@/types/api.types'
import type { KycProfile, KycSubmitPayload } from '@/types/kyc.types'
import type { AuthUser } from '@/types/auth.types'

export const profileApi = {
  /** Get current user's full profile including KYC status */
  getProfile: async (): Promise<AuthUser & { kyc?: KycProfile }> => {
    const res = await apiClient.get<ApiResponse<AuthUser & { kyc?: KycProfile }>>('/profile/me')
    return res.data.data
  },

  /** Update basic profile info */
  updateProfile: async (data: Partial<Pick<AuthUser, 'fullName' | 'avatar'>>): Promise<AuthUser> => {
    const res = await apiClient.put<ApiResponse<AuthUser>>('/profile/me', data)
    return res.data.data
  },

  /** Get KYC status for current user */
  getKycStatus: async (): Promise<KycProfile> => {
    const res = await apiClient.get<ApiResponse<KycProfile>>('/profile/kyc')
    return res.data.data
  },

  /** Submit KYC application (Step 1 + 2 + 3 combined) */
  submitKyc: async (payload: KycSubmitPayload): Promise<KycProfile> => {
    const res = await apiClient.post<ApiResponse<KycProfile>>('/profile/kyc/submit', payload)
    return res.data.data
  },

  /** Upload CCCD image (returns signed URL or stored path) */
  uploadCccdImage: async (file: File, side: 'front' | 'back'): Promise<{ url: string }> => {
    const form = new FormData()
    form.append('file', file)
    form.append('side', side)
    const res = await apiClient.post<ApiResponse<{ url: string }>>('/profile/kyc/upload', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return res.data.data
  },

  /** Re-submit after rejection */
  resubmitKyc: async (payload: KycSubmitPayload): Promise<KycProfile> => {
    const res = await apiClient.put<ApiResponse<KycProfile>>('/profile/kyc/resubmit', payload)
    return res.data.data
  },

  /** Dev mock tool: set KYC status directly */
  setMockKycStatus: async (status: KycProfile['status']): Promise<KycProfile> => {
    const res = await apiClient.post<ApiResponse<KycProfile>>('/profile/kyc/mock-status', { status })
    return res.data.data
  },
}

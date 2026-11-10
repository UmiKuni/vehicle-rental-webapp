import apiClient from '@/shared/lib/axios'
import type { KycProfile, KycSubmitPayload } from '@/types/kyc.types'
import type { AuthUser } from '@/types/auth.types'

interface BackendIdentity { userId: string; email: string; role: string }
interface BackendProfile { userId: string; email: string; phone: string; fullName: string; address: string }
interface BookingEligibility { isEligible: boolean; ineligibilityReason?: string | null }

async function currentProfile() {
  const { data: identity } = await apiClient.get<BackendIdentity>('/users/me')
  const { data: profile } = await apiClient.get<BackendProfile>(`/users/${identity.userId}/profile`)
  return { identity, profile }
}

function mapProfile(identity: BackendIdentity, profile: BackendProfile): AuthUser {
  return {
    id: identity.userId,
    username: identity.email,
    email: identity.email,
    fullName: profile.fullName || identity.email,
    role: identity.role.toUpperCase() as AuthUser['role'],
  }
}

export const profileApi = {
  getProfile: async (): Promise<AuthUser & { kyc?: KycProfile }> => {
    const { identity, profile } = await currentProfile()
    return mapProfile(identity, profile)
  },

  updateProfile: async (data: Partial<Pick<AuthUser, 'fullName' | 'avatar'>>): Promise<AuthUser> => {
    const { identity, profile } = await currentProfile()
    await apiClient.put(`/users/${identity.userId}/profile`, {
      fullName: data.fullName ?? profile.fullName,
      address: profile.address,
    })
    const { data: updated } = await apiClient.get<BackendProfile>(`/users/${identity.userId}/profile`)
    return mapProfile(identity, updated)
  },

  getKycStatus: async (): Promise<KycProfile> => {
    const { data: identity } = await apiClient.get<BackendIdentity>('/users/me')
    const { data: eligibility } = await apiClient.get<BookingEligibility>(`/users/eligibility/${identity.userId}`)
    return {
      id: identity.userId,
      userId: identity.userId,
      status: eligibility.isEligible ? 'VERIFIED' : 'UNVERIFIED',
      vehicleInterests: [],
      licenseClass: 'B2',
      hasLicense: eligibility.isEligible,
      rejectionReason: eligibility.ineligibilityReason ?? undefined,
    }
  },

  submitKyc: async (_payload: KycSubmitPayload): Promise<KycProfile> => {
    throw new Error('KYC submission is not implemented by the backend yet.')
  },

  uploadCccdImage: async (_file: File, _side: 'front' | 'back'): Promise<{ url: string }> => {
    throw new Error('KYC document upload is not implemented by the backend yet.')
  },

  resubmitKyc: async (_payload: KycSubmitPayload): Promise<KycProfile> => {
    throw new Error('KYC resubmission is not implemented by the backend yet.')
  },

  setMockKycStatus: async (_status: KycProfile['status']): Promise<KycProfile> => {
    throw new Error('Mock KYC status changes are disabled when using the real API.')
  },
}

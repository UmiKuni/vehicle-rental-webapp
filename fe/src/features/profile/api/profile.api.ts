import apiClient from '@/shared/lib/axios'
import type { KycProfile, KycSubmitPayload } from '@/types/kyc.types'
import type { AuthUser } from '@/types/auth.types'

interface BackendIdentity { userId: string; email: string; role: string }
interface BackendProfile { userId: string; email: string; phone: string; fullName: string; address: string }
interface VerificationStatus {
  userId: string
  isIdVerified: boolean
  isDriverLicenseVerified: boolean
  isVerified: boolean
}

async function currentProfile() {
  const { data: identity } = await apiClient.get<BackendIdentity>('/users/me')
  const { data: profile } = await apiClient.get<BackendProfile>(`/users/${identity.userId}/profile`)
  return { identity, profile }
}

function mapProfile(identity: BackendIdentity, profile: BackendProfile): AuthUser & { address: string; phone: string } {
  return {
    id: identity.userId,
    username: identity.email,
    email: identity.email,
    fullName: profile.fullName || identity.email,
    role: identity.role.toUpperCase() as AuthUser['role'],
    address: profile.address ?? '',
    phone: profile.phone ?? '',
  }
}

export const profileApi = {
  getProfile: async (): Promise<AuthUser & { kyc?: KycProfile; address: string; phone: string }> => {
    const { identity, profile } = await currentProfile()
    return mapProfile(identity, profile)
  },

  updateProfile: async (data: Partial<Pick<AuthUser, 'fullName' | 'avatar'>> & { address?: string }): Promise<AuthUser & { address: string; phone: string }> => {
    const { identity, profile } = await currentProfile()
    await apiClient.put(`/users/${identity.userId}/profile`, {
      fullName: data.fullName ?? profile.fullName,
      address: data.address ?? profile.address,
    })
    const { data: updated } = await apiClient.get<BackendProfile>(`/users/${identity.userId}/profile`)
    return mapProfile(identity, updated)
  },

  getKycStatus: async (): Promise<KycProfile> => {
    const { data: identity } = await apiClient.get<BackendIdentity>('/users/me')
    const { data: verification } = await apiClient.get<VerificationStatus>('/users/me/verification-status')
    return {
      id: identity.userId,
      userId: identity.userId,
      status: verification.isVerified ? 'VERIFIED' : 'UNVERIFIED',
      vehicleInterests: [],
      licenseClass: 'B2',
      hasLicense: verification.isDriverLicenseVerified,
    }
  },

  submitKyc: async (payload: KycSubmitPayload): Promise<KycProfile> => {
    const { profile } = await currentProfile()
    if (payload.step2.docType !== 'CCCD') {
      throw new Error('The current backend verification API only accepts National ID (CCCD) documents.')
    }
    await apiClient.post('/users/me/verify-id', {
      documentNumber: payload.step2.idNumber,
      fullName: profile.fullName,
      address: payload.step3.address || profile.address,
    })
    const licenseClasses: Record<string, number> = { A1: 0, A2: 1, B1: 3, B2: 4, C: 6, D: 9, E: 10, FC: 11, INTERNATIONAL: 12 }
    const licenseClass = licenseClasses[payload.step1.licenseClass] ?? 4
    await apiClient.post('/users/me/verify-driver-license', { licenseNumber: payload.licenseNumber, licenseClass })
    return profileApi.getKycStatus()
  },

  uploadCccdImage: async (file: File, side: 'front' | 'back'): Promise<{ url: string }> => {
    void file
    void side
    throw new Error('KYC document upload is not implemented by the backend yet.')
  },

  resubmitKyc: async (payload: KycSubmitPayload): Promise<KycProfile> => {
    return profileApi.submitKyc(payload)
  },

  setMockKycStatus: async (status: KycProfile['status']): Promise<KycProfile> => {
    void status
    throw new Error('Mock KYC status changes are disabled when using the real API.')
  },
}

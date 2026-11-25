import apiClient from '@/shared/lib/axios'
import type { KycProfile, KycSubmitPayload } from '@/types/kyc.types'
import type { AuthUser } from '@/types/auth.types'
import { normalizeUserRole } from '@/types/auth.types'

interface BackendIdentity { userId: string; email: string; role: string }
interface BackendProfile { userId: string; email: string; phone: string; fullName: string; address: string }
interface VerificationStatus {
  userId: string
  isIdVerified: boolean
  isDriverLicenseVerified: boolean
  isVerified: boolean
  documents: NonNullable<KycProfile['documents']>
}

async function combineImages(files: File[], name: string): Promise<File> {
  if (files.length === 1) return files[0]
  if (typeof createImageBitmap !== 'function') throw new Error('This browser cannot prepare multiple document sides for upload.')
  const bitmaps = await Promise.all(files.map((file) => createImageBitmap(file)))
  try {
    const width = Math.min(2000, Math.max(...bitmaps.map((bitmap) => bitmap.width)))
    const heights = bitmaps.map((bitmap) => Math.round((bitmap.height / bitmap.width) * width))
    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = heights.reduce((total, height) => total + height, 0)
    const context = canvas.getContext('2d')
    if (!context) throw new Error('Could not prepare the selected document images.')
    context.fillStyle = '#ffffff'
    context.fillRect(0, 0, canvas.width, canvas.height)
    let top = 0
    bitmaps.forEach((bitmap, index) => {
      context.drawImage(bitmap, 0, top, width, heights[index])
      top += heights[index]
    })
    const blob = await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob((value) => value ? resolve(value) : reject(new Error('Could not encode the selected document images.')), 'image/jpeg', 0.92)
    })
    return new File([blob], name, { type: 'image/jpeg', lastModified: Date.now() })
  } finally {
    bitmaps.forEach((bitmap) => bitmap.close())
  }
}

async function currentProfile() {
  const { data: identity } = await apiClient.get<BackendIdentity>('/users/me')
  const { data: profile } = await apiClient.get<BackendProfile>(`/users/${identity.userId}/profile`)
  return { identity, profile }
}

function mapProfile(identity: BackendIdentity, profile: BackendProfile): AuthUser & { address: string; phone: string } {
  const role = normalizeUserRole(identity.role)
  if (!role || role === 'GUEST') throw new Error('This account does not have an active application role.')
  return {
    id: identity.userId,
    username: identity.email,
    email: identity.email,
    fullName: profile.fullName || identity.email,
    role,
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
    const documents = verification.documents ?? []
    const documentStatuses = documents.map((document) => document.verificationStatus.toLowerCase())
    const rejectionReason = [...documents].reverse().find(
      (document) => document.verificationStatus.toLowerCase() === 'rejected' && document.rejectReason,
    )?.rejectReason ?? undefined
    const status = verification.isVerified
      ? 'VERIFIED'
      : documentStatuses.includes('rejected')
        ? 'REJECTED'
        : documentStatuses.includes('pending') || verification.isIdVerified || verification.isDriverLicenseVerified
          ? 'PENDING'
          : 'UNVERIFIED'
    return {
      id: identity.userId,
      userId: identity.userId,
      status,
      rejectionReason,
      vehicleInterests: [],
      licenseClass: 'B2',
      hasLicense: documents.some((document) => document.type === 'GPLX'),
      isIdVerified: verification.isIdVerified,
      isDriverLicenseVerified: verification.isDriverLicenseVerified,
      documents,
      docType: documents.some((document) => document.type === 'Passport') ? 'PASSPORT' : 'CCCD',
    }
  },

  submitKyc: async (payload: KycSubmitPayload): Promise<KycProfile> => {
    const { profile } = await currentProfile()
    if (payload.step2.docType !== 'CCCD') {
      throw new Error('The current backend verification API only accepts National ID (CCCD) documents.')
    }
    if (!payload.step2.idNumber.trim()) throw new Error('Enter your National ID number.')
    if (!payload.licenseNumber?.trim()) throw new Error("Enter your driver's license number. The backend requires a license verification.")

    let idStorageKey: string | undefined
    const idImages = [payload.step2.frontImage, payload.step2.backImage].filter((file): file is File => file instanceof File)
    if (idImages.length > 0) {
      const idDocument = await combineImages(idImages, 'national-id.jpg')
      const uploaded = await profileApi.uploadIdentityDocument(idDocument, 'CCCD')
      idStorageKey = uploaded.documents.find((document) => document.type === 'CCCD')?.storageKey
    }
    await apiClient.post('/users/me/verify-id', {
      documentNumber: payload.step2.idNumber,
      fullName: profile.fullName,
      address: payload.step3.address || profile.address,
      storageKey: idStorageKey,
    })
    const licenseClasses: Record<string, number> = { A1: 0, A2: 1, A: 2, B1: 3, B2: 4, B: 5, C: 6, D1: 7, D2: 8, D: 9, E: 10, FC: 11, INTERNATIONAL: 12 }
    const licenseClass = licenseClasses[payload.step1.licenseClass]
    if (licenseClass === undefined) throw new Error("This driver's license class is not supported by the backend.")
    let licenseStorageKey: string | undefined
    const licenseImages = [payload.licenseFrontImage, payload.licenseBackImage].filter((file): file is File => file instanceof File)
    if (licenseImages.length > 0) {
      const licenseDocument = await combineImages(licenseImages, 'drivers-license.jpg')
      const uploaded = await profileApi.uploadIdentityDocument(licenseDocument, 'GPLX')
      licenseStorageKey = uploaded.documents.find((document) => document.type === 'GPLX')?.storageKey
    }
    await apiClient.post('/users/me/verify-driver-license', {
      licenseNumber: payload.licenseNumber,
      licenseClass,
      storageKey: licenseStorageKey,
    })
    return profileApi.getKycStatus()
  },

  uploadIdentityDocument: async (file: File, type: 'CCCD' | 'GPLX' | 'Passport'): Promise<VerificationStatus> => {
    const form = new FormData()
    form.append('file', file)
    const { data } = await apiClient.post<VerificationStatus>(`/users/me/documents/${type}`, form)
    return data
  },

  resubmitKyc: async (payload: KycSubmitPayload): Promise<KycProfile> => {
    return profileApi.submitKyc(payload)
  },

}

import apiClient from '@/shared/lib/axios'
import type { KycProfile, KycSubmitPayload } from '@/types/kyc.types'
import type { AuthUser, CurrentUserResponse } from '@/types/auth.types'
import { normalizeUserRole } from '@/types/auth.types'

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

async function fetchCurrentUser(): Promise<CurrentUserResponse> {
  const { data } = await apiClient.get<CurrentUserResponse>('/users/me')
  return data
}

function mapProfile(me: CurrentUserResponse): AuthUser & { address: string; phone: string } {
  const role = normalizeUserRole(me.role)
  if (!role || role === 'GUEST') throw new Error('This account does not have an active application role.')
  return {
    id: me.userId,
    username: me.email,
    email: me.email,
    fullName: me.fullName || me.email,
    role,
    address: me.address ?? '',
    phone: me.phone ?? '',
  }
}

export const profileApi = {
  getProfile: async (): Promise<AuthUser & { kyc?: KycProfile; address: string; phone: string }> => {
    const me = await fetchCurrentUser()
    return mapProfile(me)
  },

  updateProfile: async (data: Partial<Pick<AuthUser, 'fullName' | 'avatar'>> & { address?: string }): Promise<AuthUser & { address: string; phone: string }> => {
    const me = await fetchCurrentUser()
    await apiClient.put(`/users/${me.userId}/profile`, {
      fullName: data.fullName ?? me.fullName,
      address: data.address ?? me.address,
    })
    const updated = await fetchCurrentUser()
    return mapProfile(updated)
  },

  getKycStatus: async (): Promise<KycProfile> => {
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
      id: verification.userId,
      userId: verification.userId,
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
    const me = await fetchCurrentUser()
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
      fullName: me.fullName,
      address: payload.step3.address || me.address,
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

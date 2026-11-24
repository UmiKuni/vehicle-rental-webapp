// ─── KYC / Identity Verification Types ──────────────────────────────────────

export type KycStatus =
  | 'UNVERIFIED'     // hasn't started verification
  | 'PENDING'        // submitted, awaiting admin review
  | 'VERIFIED'       // approved
  | 'REJECTED'       // rejected, can re-submit

export type LicenseClass =
  | 'A1' | 'A2'
  | 'A' | 'B1' | 'B2' | 'B'
  | 'C' | 'D1' | 'D2' | 'D' | 'E'
  | 'FC'
  | 'INTERNATIONAL' // International Driving Permit

export type IdentityDocType = 'PASSPORT' | 'CCCD'

export type VehicleInterest =
  | 'MOTORBIKE'
  | 'CAR_4_SEAT'
  | 'CAR_7_SEAT'
  | 'TRUCK'
  | 'ELECTRIC'

// Step 1 — vehicle type & license class
export interface KycStep1Data {
  vehicleInterests: VehicleInterest[]
  licenseClass: LicenseClass
  hasLicense: boolean
}

// Step 2 — Passport or CCCD
export interface KycStep2Data {
  docType: IdentityDocType
  idNumber: string
  country?: string
  expiryDate?: string
  frontImage?: File | null
  backImage?: File | null
  frontPreview?: string
  backPreview?: string
  // Legacy aliases for backward compatibility
  cccdNumber?: string
  cccdFrontImage?: File | null
  cccdBackImage?: File | null
  cccdFrontPreview?: string
  cccdBackPreview?: string
}

// Step 3 — additional personal info
export interface KycStep3Data {
  dateOfBirth: string
  address: string
  province: string
  emergencyContact: string
  emergencyPhone: string
}

// Combined submission payload
export interface KycSubmitPayload {
  step1: KycStep1Data
  licenseNumber?: string
  licenseFrontImage?: File | null
  licenseBackImage?: File | null
  step2: {
    docType: IdentityDocType
    idNumber: string
    country?: string
    expiryDate?: string
    frontPreview?: string
    backPreview?: string
    cccdNumber?: string
    frontImage?: File | null
    backImage?: File | null
  }
  step3: KycStep3Data
}

// KYC profile response from backend
export interface KycProfile {
  id: number | string
  userId: number | string
  status: KycStatus
  rejectionReason?: string
  vehicleInterests: VehicleInterest[]
  licenseClass: LicenseClass
  hasLicense: boolean
  docType?: IdentityDocType
  idNumber?: string
  country?: string
  expiryDate?: string
  cccdNumber?: string
  cccdFrontUrl?: string
  cccdBackUrl?: string
  dateOfBirth?: string
  address?: string
  province?: string
  emergencyContact?: string
  emergencyPhone?: string
  submittedAt?: string
  reviewedAt?: string
  reviewedBy?: string
  isIdVerified?: boolean
  isDriverLicenseVerified?: boolean
  documents?: Array<{
    documentId: string
    type: string
    storageKey: string
    verificationStatus: string
    rejectReason?: string | null
    accessUrl?: string | null
  }>
}

// Wizard local state
export interface KycWizardState {
  currentStep: 1 | 2 | 3
  step1: Partial<KycStep1Data>
  step2: Partial<KycStep2Data>
  step3: Partial<KycStep3Data>
}

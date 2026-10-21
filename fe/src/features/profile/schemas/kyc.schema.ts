import { z } from 'zod'

export const kycStep1Schema = z.object({
  vehicleInterests: z
    .array(z.enum(['MOTORBIKE', 'CAR_4_SEAT', 'CAR_7_SEAT', 'TRUCK', 'ELECTRIC']))
    .min(1, 'Please select at least 1 vehicle type'),
  licenseClass: z.enum(
    ['A1', 'A2', 'B1', 'B2', 'C', 'D', 'E', 'FB', 'FC', 'FD', 'FE', 'INTERNATIONAL'],
    { required_error: "Please select your driver's license class" }
  ),
  hasLicense: z.boolean(),
})

export const kycStep2Schema = z
  .object({
    docType: z.enum(['PASSPORT', 'CCCD']),
    idNumber: z.string().min(1, 'Please enter document number'),
    country: z.string().optional(),
    expiryDate: z.string().optional(),
    frontPreview: z.string().optional(),
    backPreview: z.string().optional(),
    // Backward compatibility
    cccdNumber: z.string().optional(),
    cccdFrontPreview: z.string().optional(),
    cccdBackPreview: z.string().optional(),
  })
  .superRefine((data, ctx) => {
    if (data.docType === 'PASSPORT') {
      // 1. Passport format: 6-12 alphanumeric characters
      const cleanId = data.idNumber.trim().toUpperCase()
      if (!/^[A-Z0-9]{6,12}$/.test(cleanId)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Passport number must be 6-12 alphanumeric characters (e.g. B1234567, C9876543)',
          path: ['idNumber'],
        })
      }

      // 2. Nationality / Issuing country
      if (!data.country || data.country.trim().length < 2) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Please select or enter nationality / issuing country',
          path: ['country'],
        })
      }

      // 3. Expiry date check
      if (!data.expiryDate) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Please enter passport expiry date',
          path: ['expiryDate'],
        })
      } else {
        const exp = new Date(data.expiryDate)
        const today = new Date()
        today.setHours(0, 0, 0, 0)
        if (isNaN(exp.getTime()) || exp <= today) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: 'Passport has expired. Please verify and try again.',
            path: ['expiryDate'],
          })
        }
      }
    } else {
      // CCCD: 9-12 digits
      const cleanId = data.idNumber.trim()
      if (!/^\d{9,12}$/.test(cleanId)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'National ID (CCCD/CMND) must be 9 to 12 digits',
          path: ['idNumber'],
        })
      }
    }
  })

export const kycStep3Schema = z.object({
  dateOfBirth: z.string().min(1, 'Please enter date of birth'),
  address: z.string().min(3, 'Residential / hotel address in Vietnam'),
  province: z.string().min(1, 'Please select accommodation province/city'),
  emergencyContact: z.string().min(2, 'Please enter emergency contact name'),
  emergencyPhone: z
    .string()
    .regex(/^\+?[0-9]{7,15}$/, 'Phone number must be 7-15 digits (e.g. +84912345678, +14155552671)'),
})

export type KycStep1Values = z.infer<typeof kycStep1Schema>
export type KycStep2Values = z.infer<typeof kycStep2Schema>
export type KycStep3Values = z.infer<typeof kycStep3Schema>

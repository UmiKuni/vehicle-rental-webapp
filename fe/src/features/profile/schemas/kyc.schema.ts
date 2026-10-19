import { z } from 'zod'

export const kycStep1Schema = z.object({
  vehicleInterests: z
    .array(z.enum(['MOTORBIKE', 'CAR_4_SEAT', 'CAR_7_SEAT', 'TRUCK', 'ELECTRIC']))
    .min(1, 'Chọn ít nhất 1 loại phương tiện'),
  licenseClass: z.enum(
    ['A1', 'A2', 'B1', 'B2', 'C', 'D', 'E', 'FB', 'FC', 'FD', 'FE', 'INTERNATIONAL'],
    { required_error: 'Vui lòng chọn hạng bằng lái' }
  ),
  hasLicense: z.boolean(),
})

export const kycStep2Schema = z
  .object({
    docType: z.enum(['PASSPORT', 'CCCD']),
    idNumber: z.string().min(1, 'Vui lòng nhập số giấy tờ'),
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
          message: 'Số hộ chiếu gồm 6-12 ký tự chữ và số (VD: B1234567, C9876543)',
          path: ['idNumber'],
        })
      }

      // 2. Nationality / Issuing country
      if (!data.country || data.country.trim().length < 2) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Vui lòng chọn hoặc nhập quốc tịch / quốc gia phát hành',
          path: ['country'],
        })
      }

      // 3. Expiry date check
      if (!data.expiryDate) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Vui lòng nhập ngày hết hạn hộ chiếu',
          path: ['expiryDate'],
        })
      } else {
        const exp = new Date(data.expiryDate)
        const today = new Date()
        today.setHours(0, 0, 0, 0)
        if (isNaN(exp.getTime()) || exp <= today) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: 'Hộ chiếu đã hết hạn sử dụng. Vui lòng kiểm tra lại.',
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
          message: 'CCCD/CMND phải gồm 9 đến 12 chữ số',
          path: ['idNumber'],
        })
      }
    }
  })

export const kycStep3Schema = z.object({
  dateOfBirth: z.string().min(1, 'Vui lòng nhập ngày sinh'),
  address: z.string().min(3, 'Địa chỉ lưu trú / khách sạn tại Việt Nam'),
  province: z.string().min(1, 'Vui lòng chọn tỉnh/thành lưu trú'),
  emergencyContact: z.string().min(2, 'Nhập tên người liên hệ khẩn cấp'),
  emergencyPhone: z
    .string()
    .regex(/^\+?[0-9]{7,15}$/, 'Số điện thoại gồm 7-15 chữ số (VD: +84912345678, +14155552671)'),
})

export type KycStep1Values = z.infer<typeof kycStep1Schema>
export type KycStep2Values = z.infer<typeof kycStep2Schema>
export type KycStep3Values = z.infer<typeof kycStep3Schema>

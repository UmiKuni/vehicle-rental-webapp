import { z } from 'zod'

export const kycStep1Schema = z.object({
  vehicleInterests: z
    .array(z.enum(['MOTORBIKE', 'CAR_4_SEAT', 'CAR_7_SEAT', 'TRUCK', 'ELECTRIC']))
    .min(1, 'Chọn ít nhất 1 loại phương tiện'),
  licenseClass: z.enum(['A1', 'A2', 'B1', 'B2', 'C', 'D', 'E', 'FB', 'FC', 'FD', 'FE'], {
    required_error: 'Vui lòng chọn hạng bằng lái',
  }),
  hasLicense: z.boolean(),
})

export const kycStep2Schema = z.object({
  cccdNumber: z
    .string()
    .min(9, 'CCCD/CMND tối thiểu 9 số')
    .max(12, 'CCCD/CMND tối đa 12 số')
    .regex(/^\d+$/, 'Chỉ nhập số'),
  cccdFrontPreview: z.string().optional(),
  cccdBackPreview: z.string().optional(),
})

export const kycStep3Schema = z.object({
  dateOfBirth: z.string().min(1, 'Vui lòng nhập ngày sinh'),
  address: z.string().min(5, 'Địa chỉ tối thiểu 5 ký tự'),
  province: z.string().min(1, 'Vui lòng chọn tỉnh/thành'),
  emergencyContact: z.string().min(2, 'Nhập tên người liên hệ khẩn cấp'),
  emergencyPhone: z
    .string()
    .regex(/^(0|\+84)[0-9]{8,9}$/, 'Số điện thoại không hợp lệ'),
})

export type KycStep1Values = z.infer<typeof kycStep1Schema>
export type KycStep2Values = z.infer<typeof kycStep2Schema>
export type KycStep3Values = z.infer<typeof kycStep3Schema>

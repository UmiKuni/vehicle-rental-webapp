import { z } from 'zod'

const roleEnum = z.enum(['ADMIN', 'MANAGER', 'STAFF'])
const statusEnum = z.enum(['ACTIVE', 'INACTIVE', 'BANNED'])

export const createUserSchema = z.object({
  username: z
    .string()
    .min(3, 'Tên đăng nhập tối thiểu 3 ký tự')
    .max(50, 'Tối đa 50 ký tự')
    .regex(/^[a-zA-Z0-9_]+$/, 'Chỉ chứa chữ, số và dấu gạch dưới'),
  email: z.string().email('Email không hợp lệ'),
  password: z.string().min(6, 'Mật khẩu tối thiểu 6 ký tự'),
  fullName: z.string().min(2, 'Họ tên tối thiểu 2 ký tự').max(100, 'Tối đa 100 ký tự'),
  phone: z.string().regex(/^[0-9]{9,11}$/, 'Số điện thoại không hợp lệ').optional().or(z.literal('')),
  role: roleEnum,
})

export const updateUserSchema = z.object({
  email: z.string().email('Email không hợp lệ'),
  fullName: z.string().min(2, 'Họ tên tối thiểu 2 ký tự').max(100, 'Tối đa 100 ký tự'),
  phone: z.string().regex(/^[0-9]{9,11}$/, 'Số điện thoại không hợp lệ').optional().or(z.literal('')),
  role: roleEnum,
  status: statusEnum,
})

export type CreateUserFormValues = z.infer<typeof createUserSchema>
export type UpdateUserFormValues = z.infer<typeof updateUserSchema>

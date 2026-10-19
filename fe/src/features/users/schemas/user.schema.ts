import { z } from 'zod'

const roleEnum = z.enum(['ADMIN', 'MANAGER', 'STAFF', 'CUSTOMER'])
const statusEnum = z.enum(['ACTIVE', 'INACTIVE', 'BANNED'])

export const createUserSchema = z.object({
  username: z
    .string()
    .min(3, 'Username must be at least 3 characters')
    .max(50, 'Maximum 50 characters')
    .regex(/^[a-zA-Z0-9_]+$/, 'Only letters, numbers, and underscores allowed'),
  email: z.string().email('Invalid email'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
  fullName: z.string().min(2, 'Full name must be at least 2 characters').max(100, 'Maximum 100 characters'),
  phone: z.string().regex(/^[0-9]{9,11}$/, 'Invalid phone number').optional().or(z.literal('')),
  role: roleEnum,
})

export const updateUserSchema = z.object({
  email: z.string().email('Invalid email'),
  fullName: z.string().min(2, 'Full name must be at least 2 characters').max(100, 'Maximum 100 characters'),
  phone: z.string().regex(/^[0-9]{9,11}$/, 'Invalid phone number').optional().or(z.literal('')),
  role: roleEnum,
  status: statusEnum,
})

export type CreateUserFormValues = z.infer<typeof createUserSchema>
export type UpdateUserFormValues = z.infer<typeof updateUserSchema>

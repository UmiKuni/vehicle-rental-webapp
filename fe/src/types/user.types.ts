import type { UserRole } from './auth.types'

// User response DTO (from backend)
export interface UserResponse {
  id: number
  username: string
  email: string
  fullName: string
  phone?: string
  role: UserRole
  status: UserStatus
  avatar?: string
  createdAt: string
  updatedAt: string
}

// Create user request DTO
export interface CreateUserRequest {
  username: string
  email: string
  password: string
  fullName: string
  phone?: string
  role: UserRole
}

// Update user request DTO
export interface UpdateUserRequest {
  email: string
  fullName: string
  phone?: string
  role: UserRole
  status: UserStatus
}

// Change password request DTO
export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export type UserStatus = 'ACTIVE' | 'INACTIVE' | 'BANNED'

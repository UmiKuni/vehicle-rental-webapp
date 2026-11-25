// Auth request DTOs
export interface LoginRequest {
  username: string
  password: string
}

export interface RegisterRequest {
  fullname: string
  email: string
  phone: string
  password: string
}

// Auth response DTOs
export interface TokenResponse {
  accessToken: string
  refreshToken: string
  tokenType: string
  expiresIn: number
}

export interface CurrentUserResponse {
  userId: string
  email: string
  phone: string
  fullname: string
  address: string
  role: string
  isIdVerified: boolean
  isDriverLicenseVerified: boolean
  isVerified: boolean
}

export interface AuthUser {
  id: number | string
  username: string
  email: string
  fullname: string
  role: UserRole
  avatar?: string
}

export type UserRole = 'ADMIN' | 'MANAGER' | 'STAFF' | 'OWNER' | 'CUSTOMER' | 'GUEST'

/** Normalize only roles that the backend actually defines. Unknown roles fail closed. */
export function normalizeUserRole(value: unknown): UserRole | null {
  if (typeof value !== 'string') return null
  const role = value.trim().toUpperCase()
  return ['ADMIN', 'MANAGER', 'STAFF', 'OWNER', 'CUSTOMER', 'GUEST'].includes(role)
    ? role as UserRole
    : null
}

/** Roles that have access to the admin dashboard */
export const ADMIN_ROLES: UserRole[] = ['ADMIN', 'MANAGER', 'STAFF']
/** Roles that are end-customers using the PWA */
export const CUSTOMER_ROLES: UserRole[] = ['CUSTOMER']
/** Owners use the separate owner workspace and owner-only backend endpoints. */
export const OWNER_ROLES: UserRole[] = ['OWNER']

// Refresh token request
export interface RefreshTokenRequest {
  refreshToken: string
}

// Auth context state
export interface AuthState {
  user: AuthUser | null
  isAuthenticated: boolean
  isLoading: boolean
}

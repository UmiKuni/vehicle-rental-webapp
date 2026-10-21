// Auth request DTOs
export interface LoginRequest {
  username: string
  password: string
}

export interface RegisterRequest {
  fullName: string
  username: string
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

export interface AuthUser {
  id: number
  username: string
  email: string
  fullName: string
  role: UserRole
  avatar?: string
}

export type UserRole = 'ADMIN' | 'MANAGER' | 'STAFF' | 'CUSTOMER'

/** Roles that have access to the admin dashboard */
export const ADMIN_ROLES: UserRole[] = ['ADMIN', 'MANAGER', 'STAFF']
/** Roles that are end-customers using the PWA */
export const CUSTOMER_ROLES: UserRole[] = ['CUSTOMER']

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

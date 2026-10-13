// Auth request DTOs
export interface LoginRequest {
  username: string
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

export type UserRole = 'ADMIN' | 'MANAGER' | 'STAFF'

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

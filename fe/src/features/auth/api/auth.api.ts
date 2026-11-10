import apiClient from '@/shared/lib/axios'
import { REFRESH_TOKEN_KEY, TOKEN_KEY } from '@/shared/constants'
import type { LoginRequest, RegisterRequest, TokenResponse, AuthUser, UserRole } from '@/types/auth.types'

interface BackendAuthResult {
  userId: string
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
}

interface BackendIdentity {
  userId: string
  email: string
  role: string
}

interface BackendProfile {
  userId: string
  email: string
  phone: string
  fullName: string
}

function normalizeRole(role: string): UserRole {
  switch (role.toLowerCase()) {
    case 'admin': return 'ADMIN'
    case 'manager': return 'MANAGER'
    case 'staff': return 'STAFF'
    case 'owner': return 'CUSTOMER'
    default: return 'CUSTOMER'
  }
}

async function loadSession(auth: BackendAuthResult, fullName?: string) {
  localStorage.setItem(TOKEN_KEY, auth.accessToken)
  localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)

  try {
    const { data: identity } = await apiClient.get<BackendIdentity>('/users/me')
    const { data: profile } = await apiClient.get<BackendProfile>(`/users/${identity.userId}/profile`)
    const user: AuthUser = {
      id: identity.userId,
      username: identity.email,
      email: identity.email,
      fullName: fullName || profile.fullName || identity.email,
      role: normalizeRole(identity.role),
    }
    const token: TokenResponse = {
      accessToken: auth.accessToken,
      refreshToken: auth.refreshToken,
      tokenType: 'Bearer',
      expiresIn: Math.max(0, Math.floor((Date.parse(auth.accessTokenExpiresAt) - Date.now()) / 1000)),
    }
    return { token, user }
  } catch (error) {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    throw error
  }
}

export const authApi = {
  login: async (data: LoginRequest): Promise<{ token: TokenResponse; user: AuthUser }> => {
    const { data: auth } = await apiClient.post<BackendAuthResult>('/users/login', {
      identifier: data.username,
      password: data.password,
    })
    return loadSession(auth)
  },

  register: async (data: RegisterRequest): Promise<{ token: TokenResponse; user: AuthUser }> => {
    const { data: auth } = await apiClient.post<BackendAuthResult>('/users/register', {
      email: data.email,
      phone: data.phone,
      password: data.password,
    })
    localStorage.setItem(TOKEN_KEY, auth.accessToken)
    localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)
    try {
      await apiClient.put(`/users/${auth.userId}/profile`, {
        fullName: data.fullName,
        address: '',
      })
      return loadSession(auth, data.fullName)
    } catch (error) {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(REFRESH_TOKEN_KEY)
      throw error
    }
  },

  logout: async (): Promise<void> => {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
    if (refreshToken) await apiClient.post('/users/logout', { refreshToken })
  },

  getMe: async (): Promise<AuthUser> => {
    const { data: identity } = await apiClient.get<BackendIdentity>('/users/me')
    const { data: profile } = await apiClient.get<BackendProfile>(`/users/${identity.userId}/profile`)
    return {
      id: identity.userId,
      username: identity.email,
      email: identity.email,
      fullName: profile.fullName || identity.email,
      role: normalizeRole(identity.role),
    }
  },
}

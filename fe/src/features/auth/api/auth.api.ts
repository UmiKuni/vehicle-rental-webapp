import apiClient from '@/shared/lib/axios'
import { REFRESH_TOKEN_KEY, TOKEN_KEY } from '@/shared/constants'
import { normalizeUserRole } from '@/types/auth.types'
import type {
  LoginRequest,
  RegisterRequest,
  TokenResponse,
  AuthUser,
  CurrentUserResponse,
} from '@/types/auth.types'

interface BackendAuthResult {
  userId: string
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
}

function getRole(role: string) {
  const normalized = normalizeUserRole(role)
  if (!normalized || normalized === 'GUEST') throw new Error('This account does not have an active customer, owner or admin role.')
  return normalized
}

async function loadSession(auth: BackendAuthResult, fullName?: string) {
  localStorage.setItem(TOKEN_KEY, auth.accessToken)
  localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)

  try {
    const { data: me } = await apiClient.get<CurrentUserResponse>('/users/me')
    const user: AuthUser = {
      id: me.userId,
      username: me.email,
      email: me.email,
      fullName: fullName || me.fullName || me.email,
      role: getRole(me.role),
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
    const { data: me } = await apiClient.get<CurrentUserResponse>('/users/me')
    return {
      id: me.userId,
      username: me.email,
      email: me.email,
      fullName: me.fullName || me.email,
      role: getRole(me.role),
    }
  },
}

import apiClient from '@/shared/lib/axios'
import type { ApiResponse } from '@/types/api.types'
import type { LoginRequest, TokenResponse, AuthUser } from '@/types/auth.types'

export const authApi = {
  login: async (data: LoginRequest): Promise<{ token: TokenResponse; user: AuthUser }> => {
    const res = await apiClient.post<ApiResponse<{ token: TokenResponse; user: AuthUser }>>(
      '/auth/login',
      data
    )
    return res.data.data
  },

  logout: async (): Promise<void> => {
    await apiClient.post('/auth/logout')
  },

  getMe: async (): Promise<AuthUser> => {
    const res = await apiClient.get<ApiResponse<AuthUser>>('/auth/me')
    return res.data.data
  },
}

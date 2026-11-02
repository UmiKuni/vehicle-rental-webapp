import apiClient from '@/shared/lib/axios'
import type { ApiResponse, PaginatedResponse, PaginationParams } from '@/types/api.types'
import type { UserResponse, CreateUserRequest, UpdateUserRequest } from '@/types/user.types'

export const userApi = {
  getAll: async (params?: PaginationParams & { search?: string }): Promise<PaginatedResponse<UserResponse>> => {
    const res = await apiClient.get<ApiResponse<PaginatedResponse<UserResponse>>>('/users', { params })
    return res.data.data
  },

  getById: async (id: number): Promise<UserResponse> => {
    const res = await apiClient.get<ApiResponse<UserResponse>>(`/users/${id}`)
    return res.data.data
  },

  create: async (data: CreateUserRequest): Promise<UserResponse> => {
    const res = await apiClient.post<ApiResponse<UserResponse>>('/users', data)
    return res.data.data
  },

  update: async (id: number, data: UpdateUserRequest): Promise<UserResponse> => {
    const res = await apiClient.put<ApiResponse<UserResponse>>(`/users/${id}`, data)
    return res.data.data
  },

  delete: async (id: number): Promise<void> => {
    await apiClient.delete(`/users/${id}`)
  },
}

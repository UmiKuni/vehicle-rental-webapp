import { useMutation, useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { queryClient, getErrorMessage } from '@/shared/lib/query-client'
import { userApi } from '../api/user.api'
import { userQueryKeys } from '../api/user.query-keys'
import type { PaginationParams } from '@/types/api.types'
import type { CreateUserRequest, UpdateUserRequest } from '@/types/user.types'

// ---- Queries ----

export function useUsers(params?: PaginationParams & { search?: string }) {
  return useQuery({
    queryKey: userQueryKeys.list(params),
    queryFn: () => userApi.getAll(params),
    staleTime: 5 * 60 * 1000,
    placeholderData: (prev) => prev,
  })
}

export function useUser(id: number) {
  return useQuery({
    queryKey: userQueryKeys.detail(id),
    queryFn: () => userApi.getById(id),
    enabled: id > 0,
  })
}

// ---- Mutations ----

export function useCreateUser() {
  return useMutation({
    mutationFn: (data: CreateUserRequest) => userApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: userQueryKeys.lists() })
      toast.success('User created successfully!')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })
}

export function useUpdateUser(id: number) {
  return useMutation({
    mutationFn: (data: UpdateUserRequest) => userApi.update(id, data),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: userQueryKeys.lists() })
      queryClient.setQueryData(userQueryKeys.detail(id), updated)
      toast.success('User updated successfully!')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })
}

export function useDeleteUser() {
  return useMutation({
    mutationFn: (id: number) => userApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: userQueryKeys.lists() })
      toast.success('User deleted successfully!')
    },
    onError: (error) => toast.error(getErrorMessage(error)),
  })
}

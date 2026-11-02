import { QUERY_KEYS } from '@/shared/constants'
import type { PaginationParams } from '@/types/api.types'

export const userQueryKeys = {
  all: [QUERY_KEYS.USERS] as const,
  lists: () => [...userQueryKeys.all, 'list'] as const,
  list: (params?: PaginationParams & { search?: string }) =>
    [...userQueryKeys.lists(), params] as const,
  details: () => [...userQueryKeys.all, 'detail'] as const,
  detail: (id: number) => [...userQueryKeys.details(), id] as const,
}

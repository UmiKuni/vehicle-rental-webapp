export const APP_NAME = import.meta.env.VITE_APP_NAME ?? 'Vehicle Rental'

export const ROUTES = {
  LOGIN: '/login',
  DASHBOARD: '/',
  USERS: '/users',
  NOT_FOUND: '/404',
} as const

export const QUERY_KEYS = {
  USERS: 'users',
  USER: 'user',
  DASHBOARD_STATS: 'dashboardStats',
} as const

export const DEFAULT_PAGE_SIZE = 10
export const PAGE_SIZE_OPTIONS = [10, 25, 50] as const

export const TOKEN_KEY = 'access_token'
export const REFRESH_TOKEN_KEY = 'refresh_token'

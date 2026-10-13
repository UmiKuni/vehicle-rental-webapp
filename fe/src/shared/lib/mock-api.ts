/**
 * Mock API interceptor — only active when VITE_APP_ENV=development
 * and there's no real backend.
 *
 * Intercepts specific Axios requests and returns realistic fake data.
 * Remove this file (and its import in main.tsx) when connecting to real backend.
 */
import apiClient from '@/shared/lib/axios'
import type { AxiosResponse } from 'axios'
import type { ApiResponse, PaginatedResponse } from '@/types/api.types'
import type { UserResponse } from '@/types/user.types'
import type { AuthUser, TokenResponse } from '@/types/auth.types'

const MOCK_USERS: UserResponse[] = Array.from({ length: 35 }, (_, i) => ({
  id: i + 1,
  username: `user${String(i + 1).padStart(3, '0')}`,
  email: `user${i + 1}@example.com`,
  fullName: ['Nguyễn Văn An', 'Trần Thị Bình', 'Lê Minh Cường', 'Phạm Thu Hà', 'Hoàng Đức Mạnh'][i % 5],
  phone: `09${String(Math.floor(Math.random() * 100000000)).padStart(8, '0')}`,
  role: (['ADMIN', 'MANAGER', 'STAFF'] as const)[i % 3],
  status: (['ACTIVE', 'ACTIVE', 'ACTIVE', 'INACTIVE', 'BANNED'] as const)[i % 5],
  avatar: undefined,
  createdAt: new Date(Date.now() - i * 86400000 * 3).toISOString(),
  updatedAt: new Date(Date.now() - i * 86400000).toISOString(),
}))

function mockResponse<T>(data: T): AxiosResponse<ApiResponse<T>> {
  return {
    data: { success: true, message: 'OK', data },
    status: 200,
    statusText: 'OK',
    headers: {},
    config: {} as never,
  }
}

function delay(ms = 300) {
  return new Promise((r) => setTimeout(r, ms))
}

// Only install mock when there's no real backend configured
const isDev = import.meta.env.VITE_APP_ENV === 'development'

if (isDev) {
  apiClient.interceptors.request.use(async (config) => {
    const url = config.url ?? ''

    await delay(300)

    // Auth — login
    if (url.includes('/auth/login') && config.method === 'post') {
      const mockToken: TokenResponse = {
        accessToken: buildFakeJwt(),
        refreshToken: 'mock-refresh-token',
        tokenType: 'Bearer',
        expiresIn: 3600,
      }
      const mockUser: AuthUser = {
        id: 1,
        username: 'admin',
        email: 'admin@vehiclerental.vn',
        fullName: 'Super Admin',
        role: 'ADMIN',
      }
      // Directly resolve the request
      config.adapter = async () => mockResponse({ token: mockToken, user: mockUser })
      return config
    }

    // Users — list
    if (url.includes('/users') && !url.match(/\/users\/\d+/) && config.method === 'get') {
      const params = config.params ?? {}
      const page = Number(params.page ?? 0)
      const size = Number(params.size ?? 10)
      const search: string = (params.search ?? '').toLowerCase()

      const filtered = search
        ? MOCK_USERS.filter(
            (u) =>
              u.fullName.toLowerCase().includes(search) ||
              u.username.toLowerCase().includes(search) ||
              u.email.toLowerCase().includes(search)
          )
        : MOCK_USERS

      const paged: PaginatedResponse<UserResponse> = {
        content: filtered.slice(page * size, page * size + size),
        totalElements: filtered.length,
        totalPages: Math.ceil(filtered.length / size),
        size,
        number: page,
        first: page === 0,
        last: page >= Math.ceil(filtered.length / size) - 1,
        empty: filtered.length === 0,
      }
      config.adapter = async () => mockResponse(paged)
      return config
    }

    // Users — get by id
    const userDetailMatch = url.match(/\/users\/(\d+)$/)
    if (userDetailMatch && config.method === 'get') {
      const id = Number(userDetailMatch[1])
      const user = MOCK_USERS.find((u) => u.id === id)
      config.adapter = async () =>
        user
          ? mockResponse(user)
          : { data: { success: false, message: 'Không tìm thấy người dùng' }, status: 404, statusText: 'Not Found', headers: {}, config: {} as never }
      return config
    }

    // Users — create
    if (url.includes('/users') && config.method === 'post') {
      const body = JSON.parse(config.data ?? '{}')
      const newUser: UserResponse = {
        id: Date.now(),
        ...body,
        status: 'ACTIVE',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      }
      MOCK_USERS.unshift(newUser)
      config.adapter = async () => mockResponse(newUser)
      return config
    }

    // Users — update
    const userUpdateMatch = url.match(/\/users\/(\d+)$/)
    if (userUpdateMatch && config.method === 'put') {
      const id = Number(userUpdateMatch[1])
      const idx = MOCK_USERS.findIndex((u) => u.id === id)
      const body = JSON.parse(config.data ?? '{}')
      if (idx !== -1) {
        MOCK_USERS[idx] = { ...MOCK_USERS[idx], ...body, updatedAt: new Date().toISOString() }
        config.adapter = async () => mockResponse(MOCK_USERS[idx])
      }
      return config
    }

    // Users — delete
    const userDeleteMatch = url.match(/\/users\/(\d+)$/)
    if (userDeleteMatch && config.method === 'delete') {
      const id = Number(userDeleteMatch[1])
      const idx = MOCK_USERS.findIndex((u) => u.id === id)
      if (idx !== -1) MOCK_USERS.splice(idx, 1)
      config.adapter = async () => ({
        data: { success: true, message: 'Deleted' },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: {} as never,
      })
      return config
    }

    // Auth — logout
    if (url.includes('/auth/logout')) {
      config.adapter = async () => mockResponse(null)
      return config
    }

    return config
  })
}

function buildFakeJwt(): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(
    JSON.stringify({
      sub: '1',
      user: { id: 1, username: 'admin', email: 'admin@vehiclerental.vn', fullName: 'Super Admin', role: 'ADMIN' },
      exp: Math.floor(Date.now() / 1000) + 3600,
      iat: Math.floor(Date.now() / 1000),
    })
  )
  const sig = btoa('mock-signature')
  return `${header}.${payload}.${sig}`
}

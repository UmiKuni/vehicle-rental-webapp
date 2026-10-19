/**
 * Mock API interceptor — only active when VITE_APP_ENV=development
 * and there's no real backend.
 *
 * Intercepts specific Axios requests and returns realistic fake data.
 * Remove this file (and its import in main.tsx) when connecting to real backend.
 *
 * Django REST API URL conventions:
 *   POST   /api/auth/login        → login
 *   POST   /api/auth/logout       → logout
 *   GET    /api/profile/me        → get profile + KYC
 *   GET    /api/profile/kyc       → get KYC status
 *   POST   /api/profile/kyc/submit → submit KYC
 *   POST   /api/profile/kyc/upload → upload CCCD images
 *   GET    /api/users             → list users (paginated)
 *   POST   /api/users             → create user
 *   GET    /api/users/:id         → get user by id
 *   PUT    /api/users/:id         → update user
 *   DELETE /api/users/:id         → delete user
 */
import apiClient from '@/shared/lib/axios'
import type { AxiosResponse } from 'axios'
import type { ApiResponse, PaginatedResponse } from '@/types/api.types'
import type { UserResponse } from '@/types/user.types'
import type { AuthUser, TokenResponse } from '@/types/auth.types'
import type { KycProfile } from '@/types/kyc.types'
import { safeBase64Encode } from '@/shared/utils'

function parsePayload(data: unknown): Record<string, any> {
  if (!data) return {}
  if (typeof data === 'object') return data as Record<string, any>
  if (typeof data === 'string') {
    try {
      return JSON.parse(data)
    } catch {
      return {}
    }
  }
  return {}
}

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

// ─── Mock KYC State (mutable per session) ────────────────────────────────────
let CURRENT_MOCK_USER: AuthUser = {
  id: 4,
  username: 'user',
  email: 'user@gmail.com',
  fullName: 'Lê Văn Khách',
  role: 'CUSTOMER',
}

let MOCK_KYC: KycProfile = {
  id: 1,
  userId: 1,
  status: 'UNVERIFIED',
}

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
      const body = parsePayload(config.data)
      const input = String(body.username ?? '').trim().toLowerCase()

      const MOCK_ACCOUNTS: Record<string, AuthUser> = {
        admin:   { id: 1, username: 'admin',   email: 'admin@vehiclerental.vn',   fullName: 'Super Admin',     role: 'ADMIN'    },
        manager: { id: 2, username: 'manager', email: 'manager@vehiclerental.vn', fullName: 'Nguyễn Văn Quản', role: 'MANAGER'  },
        staff:   { id: 3, username: 'staff',   email: 'staff@vehiclerental.vn',   fullName: 'Trần Thị Nhân',   role: 'STAFF'    },
        user:    { id: 4, username: 'user',    email: 'user@gmail.com',            fullName: 'Lê Văn Khách',    role: 'CUSTOMER' },
      }

      let mockUser: AuthUser
      if (input === 'admin') {
        mockUser = MOCK_ACCOUNTS['admin']
      } else if (input === 'manager') {
        mockUser = MOCK_ACCOUNTS['manager']
      } else if (input === 'staff') {
        mockUser = MOCK_ACCOUNTS['staff']
      } else if (input === 'user' || input.includes('user') || input.includes('khach') || input.includes('customer')) {
        mockUser = MOCK_ACCOUNTS['user']
      } else {
        const byEmail = Object.values(MOCK_ACCOUNTS).find((a) => a.email.toLowerCase() === input)
        mockUser = byEmail ?? (input === '' ? MOCK_ACCOUNTS['admin'] : MOCK_ACCOUNTS['user'])
      }

      CURRENT_MOCK_USER = mockUser
      const mockToken: TokenResponse = {
        accessToken: buildFakeJwt(mockUser),
        refreshToken: 'mock-refresh-token',
        tokenType: 'Bearer',
        expiresIn: 3600,
      }
      // Directly resolve the request
      config.adapter = async () => mockResponse({ token: mockToken, user: mockUser })
      return config
    }

    // Auth — register
    if (url.includes('/auth/register') && config.method === 'post') {
      const body = parsePayload(config.data)
      const newUser: AuthUser = {
        id: Date.now(),
        username: String(body.username ?? 'user_new'),
        email: String(body.email ?? 'user@gmail.com'),
        fullName: String(body.fullName ?? 'Khách hàng mới'),
        role: 'CUSTOMER',
      }
      CURRENT_MOCK_USER = newUser
      MOCK_KYC = {
        id: Date.now(),
        userId: newUser.id,
        status: 'UNVERIFIED',
      }
      const mockToken: TokenResponse = {
        accessToken: buildFakeJwt(newUser),
        refreshToken: 'mock-refresh-token',
        tokenType: 'Bearer',
        expiresIn: 3600,
      }
      config.adapter = async () => mockResponse({ token: mockToken, user: newUser })
      return config
    }

    // Auth — get current user
    if (url.includes('/auth/me') && config.method === 'get') {
      config.adapter = async () => mockResponse(CURRENT_MOCK_USER)
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
      const body = parsePayload(config.data)
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
      const body = parsePayload(config.data)
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

    // Profile — get my profile
    if (url.includes('/profile/me') && config.method === 'get') {
      const profile = {
        ...CURRENT_MOCK_USER,
        kyc: MOCK_KYC,
      }
      config.adapter = async () => mockResponse(profile)
      return config
    }

    // KYC — mock status switcher for testing
    if (url.includes('/profile/kyc/mock-status') && config.method === 'post') {
      const body = parsePayload(config.data)
      const nextStatus = body.status ?? 'UNVERIFIED'
      MOCK_KYC = {
        ...MOCK_KYC,
        status: nextStatus,
        rejectionReason:
          nextStatus === 'REJECTED'
            ? 'Ảnh CCCD mặt sau bị mờ, không nhận diện được số ID.'
            : undefined,
        cccdNumber: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.cccdNumber ?? '079201008899') : undefined,
        province: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.province ?? 'TP. Hồ Chí Minh') : undefined,
        dateOfBirth: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.dateOfBirth ?? '1998-05-15') : undefined,
        submittedAt: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.submittedAt ?? new Date().toISOString()) : undefined,
      }
      config.adapter = async () => mockResponse(MOCK_KYC)
      return config
    }

    // KYC — get status
    if (url.includes('/profile/kyc') && !url.includes('submit') && !url.includes('upload') && !url.includes('resubmit') && config.method === 'get') {
      config.adapter = async () => mockResponse(MOCK_KYC)
      return config
    }

    // KYC — submit
    if (url.includes('/profile/kyc/submit') && config.method === 'post') {
      const body = parsePayload(config.data)
      MOCK_KYC = {
        ...MOCK_KYC,
        status: 'PENDING',
        vehicleInterests: body.step1?.vehicleInterests ?? [],
        licenseClass: body.step1?.licenseClass ?? 'B1',
        hasLicense: body.step1?.hasLicense ?? false,
        cccdNumber: body.step2?.cccdNumber,
        dateOfBirth: body.step3?.dateOfBirth,
        address: body.step3?.address,
        province: body.step3?.province,
        emergencyContact: body.step3?.emergencyContact,
        emergencyPhone: body.step3?.emergencyPhone,
        submittedAt: new Date().toISOString(),
      }
      config.adapter = async () => mockResponse(MOCK_KYC)
      return config
    }

    // KYC — upload image
    if (url.includes('/profile/kyc/upload') && config.method === 'post') {
      config.adapter = async () => mockResponse({ url: `https://placehold.co/400x250/1a8a9d/fff?text=CCCD` })
      return config
    }

    // KYC — resubmit
    if (url.includes('/profile/kyc/resubmit') && config.method === 'put') {
      MOCK_KYC = { ...MOCK_KYC, status: 'PENDING', submittedAt: new Date().toISOString() }
      config.adapter = async () => mockResponse(MOCK_KYC)
      return config
    }

    return config
  })
}

function buildFakeJwt(user?: AuthUser): string {
  const targetUser = user ?? CURRENT_MOCK_USER
  const header = safeBase64Encode(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = safeBase64Encode(
    JSON.stringify({
      sub: String(targetUser.id),
      user: targetUser,
      exp: Math.floor(Date.now() / 1000) + 3600,
      iat: Math.floor(Date.now() / 1000),
    })
  )
  const sig = safeBase64Encode('mock-signature')
  return `${header}.${payload}.${sig}`
}

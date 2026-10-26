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
import type { Vehicle } from '@/types/vehicle.types'
import type { Booking } from '@/types/booking.types'
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
  fullName: ['Alex Johnson', 'Sarah Williams', 'Michael Brown', 'Emily Davis', 'David Miller'][i % 5],
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
  fullName: 'John Customer',
  role: 'CUSTOMER',
}

let MOCK_KYC: KycProfile = {
  id: 1,
  userId: 1,
  status: 'UNVERIFIED',
  docType: 'PASSPORT',
  idNumber: 'P12345678',
  country: 'United States',
  expiryDate: '2028-12-31',
  vehicleInterests: ['CAR_4_SEAT', 'ELECTRIC'],
  licenseClass: 'INTERNATIONAL',
  hasLicense: true,
}

const MOCK_VEHICLES: Vehicle[] = [
  {
    id: 'v1',
    name: 'VinFast VF 8 Plus',
    brand: 'VinFast',
    modelYear: 2024,
    category: 'ELECTRIC',
    transmission: 'AUTOMATIC',
    fuelType: 'ELECTRIC',
    seats: 5,
    pricePerDay: 1200000,
    imageUrl: 'https://images.unsplash.com/photo-1617788138017-80ad40651399?auto=format&fit=crop&w=800&q=80',
    rating: 4.95,
    tripsCount: 138,
    location: 'District 1, HCMC',
    features: ['Range 420km/charge', 'ADAS Level 2 Copilot', '360° Camera', 'Ultra-fast charging', 'Apple CarPlay'],
    isAvailable: true,
    depositAmount: 5000000,
  },
  {
    id: 'v2',
    name: 'Mazda CX-5 2.0 Premium',
    brand: 'Mazda',
    modelYear: 2023,
    category: 'CAR_4_SEAT',
    transmission: 'AUTOMATIC',
    fuelType: 'GASOLINE',
    seats: 5,
    pricePerDay: 950000,
    imageUrl: 'https://images.unsplash.com/photo-1549399542-7e3f8b79c341?auto=format&fit=crop&w=800&q=80',
    rating: 4.88,
    tripsCount: 245,
    location: 'Binh Thanh, HCMC',
    features: ['Sunroof', '10-speaker Bose sound system', 'Collision sensors', 'Nappa leather seats', 'Cruise Control'],
    isAvailable: true,
    depositAmount: 4000000,
  },
  {
    id: 'v3',
    name: 'Toyota Fortuner Legender',
    brand: 'Toyota',
    modelYear: 2023,
    category: 'CAR_7_SEAT',
    transmission: 'AUTOMATIC',
    fuelType: 'DIESEL',
    seats: 7,
    pricePerDay: 1450000,
    imageUrl: 'https://images.unsplash.com/photo-1533473359331-0135ef1b58bf?auto=format&fit=crop&w=800&q=80',
    rating: 4.92,
    tripsCount: 92,
    location: 'Tan Binh, HCMC',
    features: ['4WD dual-axle drive', 'Spacious 3rd row', 'Fuel efficient', 'Rearview camera', 'Power tailgate'],
    isAvailable: true,
    depositAmount: 5000000,
  },
  {
    id: 'v4',
    name: 'VinFast VF 5 Plus',
    brand: 'VinFast',
    modelYear: 2024,
    category: 'ELECTRIC',
    transmission: 'AUTOMATIC',
    fuelType: 'ELECTRIC',
    seats: 5,
    pricePerDay: 650000,
    imageUrl: 'https://images.unsplash.com/photo-1552519507-da3b142c6e3d?auto=format&fit=crop&w=800&q=80',
    rating: 4.86,
    tripsCount: 180,
    location: 'District 7, HCMC',
    features: ['Compact city size', 'Economical charging', 'Touchscreen display', 'Automatic emergency braking'],
    isAvailable: true,
    depositAmount: 3000000,
  },
  {
    id: 'v5',
    name: 'Hyundai Tucson 2.0',
    brand: 'Hyundai',
    modelYear: 2023,
    category: 'CAR_4_SEAT',
    transmission: 'AUTOMATIC',
    fuelType: 'GASOLINE',
    seats: 5,
    pricePerDay: 1050000,
    imageUrl: 'https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=800&q=80',
    rating: 4.91,
    tripsCount: 115,
    location: 'Thu Duc, HCMC',
    features: ['SmartSense safety suite', 'Push-button shifter', 'Wireless charger', 'Rear AC vents'],
    isAvailable: true,
    depositAmount: 4000000,
  },
  {
    id: 'v6',
    name: 'Mitsubishi Xpander',
    brand: 'Mitsubishi',
    modelYear: 2023,
    category: 'CAR_7_SEAT',
    transmission: 'AUTOMATIC',
    fuelType: 'GASOLINE',
    seats: 7,
    pricePerDay: 850000,
    imageUrl: 'https://images.unsplash.com/photo-1502877338535-766e1452684a?auto=format&fit=crop&w=800&q=80',
    rating: 4.89,
    tripsCount: 320,
    location: 'Go Vap, HCMC',
    features: ['Fold-flat 7 seats', 'Spacious trunk', 'Fuel efficient', 'Electronic parking brake'],
    isAvailable: true,
    depositAmount: 3500000,
  },
  {
    id: 'v7',
    name: 'Ford Ranger Wildtrak',
    brand: 'Ford',
    modelYear: 2023,
    category: 'TRUCK',
    transmission: 'AUTOMATIC',
    fuelType: 'DIESEL',
    seats: 5,
    pricePerDay: 1150000,
    imageUrl: 'https://images.unsplash.com/photo-1559416523-140ddc3d238c?auto=format&fit=crop&w=800&q=80',
    rating: 4.94,
    tripsCount: 84,
    location: 'District 2, HCMC',
    features: ['Bi-Turbo engine', 'Large cargo bed', 'Electronic transfer case', '12-inch touchscreen'],
    isAvailable: true,
    depositAmount: 5000000,
  },
  {
    id: 'v8',
    name: 'Honda SH 160i ABS',
    brand: 'Honda',
    modelYear: 2024,
    category: 'MOTORBIKE',
    transmission: 'AUTOMATIC',
    fuelType: 'GASOLINE',
    seats: 2,
    pricePerDay: 250000,
    imageUrl: 'https://images.unsplash.com/photo-1558981403-c5f9899a28bc?auto=format&fit=crop&w=800&q=80',
    rating: 4.97,
    tripsCount: 450,
    location: 'District 1, HCMC',
    features: ['Dual-channel ABS', '28L underseat trunk', 'Smart keyless entry', 'USB charging port'],
    isAvailable: true,
    depositAmount: 1000000,
  },
]

const MOCK_BOOKINGS: Booking[] = [
  {
    id: 'b1',
    bookingCode: 'BK-20261025',
    vehicleId: 'v1',
    vehicle: MOCK_VEHICLES[0],
    userId: 4,
    customerName: 'John Customer',
    customerPhone: '0912345678',
    startDate: '2026-10-25',
    endDate: '2026-10-27',
    totalDays: 2,
    pricePerDay: 1200000,
    totalAmount: 2400000,
    depositAmount: 5000000,
    deliveryType: 'STATION',
    status: 'CONFIRMED',
    createdAt: '2026-10-18T10:30:00.000Z',
    note: 'Pick up at VinFast Charging Station, District 1',
  },
]

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
        admin:   { id: 1, username: 'admin',   email: 'admin@vehiclerental.com',   fullName: 'Super Admin',     role: 'ADMIN'    },
        manager: { id: 2, username: 'manager', email: 'manager@vehiclerental.com', fullName: 'Michael Manager', role: 'MANAGER'  },
        staff:   { id: 3, username: 'staff',   email: 'staff@vehiclerental.com',   fullName: 'Sarah Staff',     role: 'STAFF'    },
        user:    { id: 4, username: 'user',    email: 'user@gmail.com',            fullName: 'John Customer',    role: 'CUSTOMER' },
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
        fullName: String(body.fullName ?? 'New Customer'),
        role: 'CUSTOMER',
      }
      CURRENT_MOCK_USER = newUser
      MOCK_KYC = {
        id: Date.now(),
        userId: newUser.id,
        status: 'UNVERIFIED',
        vehicleInterests: [],
        licenseClass: 'B2',
        hasLicense: false,
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
          : { data: { success: false, message: 'User not found' }, status: 404, statusText: 'Not Found', headers: {}, config: {} as never }
      return config
    }

    // Users — create
    if (url.includes('/users') && config.method === 'post') {
      const body = parsePayload(config.data)
      const newUser: UserResponse = {
        id: Date.now(),
        username: String(body.username ?? `user_${Date.now()}`),
        email: String(body.email ?? `user_${Date.now()}@example.com`),
        fullName: String(body.fullName ?? 'New User'),
        phone: body.phone,
        role: body.role ?? 'STAFF',
        status: 'ACTIVE',
        avatar: undefined,
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
            ? 'Passport photo has glare, ID number is unreadable.'
            : undefined,
        docType: MOCK_KYC.docType ?? 'PASSPORT',
        idNumber: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.idNumber ?? 'P98765432') : undefined,
        country: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.country ?? 'United States') : undefined,
        expiryDate: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.expiryDate ?? '2029-06-30') : undefined,
        cccdNumber: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.cccdNumber ?? 'P98765432') : undefined,
        province: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.province ?? 'Ho Chi Minh City') : undefined,
        dateOfBirth: nextStatus !== 'UNVERIFIED' ? (MOCK_KYC.dateOfBirth ?? '1995-08-20') : undefined,
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
        licenseClass: body.step1?.licenseClass ?? 'INTERNATIONAL',
        hasLicense: body.step1?.hasLicense ?? false,
        docType: body.step2?.docType ?? 'PASSPORT',
        idNumber: body.step2?.idNumber ?? body.step2?.cccdNumber,
        country: body.step2?.country,
        expiryDate: body.step2?.expiryDate,
        cccdNumber: body.step2?.idNumber ?? body.step2?.cccdNumber,
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

    // Vehicles — list with filters
    if (url.includes('/vehicles') && !url.match(/\/vehicles\/[a-zA-Z0-9_-]+$/) && config.method === 'get') {
      const params = config.params ?? {}
      const category = params.category
      const search = (params.search ?? '').toLowerCase().trim()
      const transmission = params.transmission
      const brand = (params.brand ?? '').toLowerCase().trim()
      const location = (params.location ?? '').toLowerCase().trim()
      const minPrice = Number(params.minPrice ?? 0)
      const maxPrice = Number(params.maxPrice ?? Number.POSITIVE_INFINITY)
      const seats = Number(params.seats ?? 0)

      let list = [...MOCK_VEHICLES]
      if (category && category !== 'ALL') {
        list = list.filter((v) => v.category === category)
      }
      if (transmission && transmission !== 'ALL') {
        list = list.filter((v) => v.transmission === transmission)
      }
      if (brand) list = list.filter((v) => v.brand.toLowerCase().includes(brand))
      if (location) {
        const normalizedLocation = location.replace('ho chi minh city', 'hcmc').replace('tp. hồ chí minh', 'hcmc')
        list = list.filter((v) => v.location.toLowerCase().includes(normalizedLocation))
      }
      if (minPrice) list = list.filter((v) => v.pricePerDay >= minPrice)
      if (Number.isFinite(maxPrice)) list = list.filter((v) => v.pricePerDay <= maxPrice)
      if (seats) list = list.filter((v) => v.seats >= seats)
      if (search) {
        list = list.filter(
          (v) =>
            v.name.toLowerCase().includes(search) ||
            v.brand.toLowerCase().includes(search) ||
            v.location.toLowerCase().includes(search)
        )
      }
      config.adapter = async () => mockResponse(list)
      return config
    }

    // Vehicles — get by ID
    const vehicleDetailMatch = url.match(/\/vehicles\/([a-zA-Z0-9_-]+)$/)
    if (vehicleDetailMatch && config.method === 'get') {
      const id = vehicleDetailMatch[1]
      const vehicle = MOCK_VEHICLES.find((v) => v.id === id)
      config.adapter = async () =>
        vehicle
          ? mockResponse(vehicle)
          : { data: { success: false, message: 'Vehicle not found' }, status: 404, statusText: 'Not Found', headers: {}, config: {} as never }
      return config
    }

    // Bookings — list
    if (url.includes('/bookings') && !url.includes('/cancel') && config.method === 'get') {
      config.adapter = async () => mockResponse(MOCK_BOOKINGS)
      return config
    }

    // Bookings — create
    if (url.includes('/bookings') && config.method === 'post') {
      const body = parsePayload(config.data)
      const vehicle = MOCK_VEHICLES.find((v) => v.id === body.vehicleId) ?? MOCK_VEHICLES[0]
      const start = new Date(body.startDate)
      const end = new Date(body.endDate)
      const diffTime = Math.abs(end.getTime() - start.getTime())
      const totalDays = Math.max(1, Math.ceil(diffTime / (1000 * 60 * 60 * 24)))
      const totalAmount = totalDays * vehicle.pricePerDay

      const newBooking: Booking = {
        id: `b_${Date.now()}`,
        bookingCode: `BK-${Math.floor(100000 + Math.random() * 900000)}`,
        vehicleId: vehicle.id,
        vehicle,
        userId: CURRENT_MOCK_USER.id,
        customerName: body.customerName || CURRENT_MOCK_USER.fullName,
        customerPhone: body.customerPhone || '0912345678',
        startDate: body.startDate,
        endDate: body.endDate,
        totalDays,
        pricePerDay: vehicle.pricePerDay,
        totalAmount,
        depositAmount: vehicle.depositAmount,
        deliveryType: body.deliveryType ?? 'STATION',
        deliveryAddress: body.deliveryAddress,
        status: 'CONFIRMED',
        createdAt: new Date().toISOString(),
        note: body.note,
      }
      MOCK_BOOKINGS.unshift(newBooking)
      config.adapter = async () => mockResponse(newBooking)
      return config
    }

    // Bookings — cancel
    const cancelMatch = url.match(/\/bookings\/([a-zA-Z0-9_-]+)\/cancel$/)
    if (cancelMatch && config.method === 'put') {
      const id = cancelMatch[1]
      const booking = MOCK_BOOKINGS.find((b) => b.id === id)
      if (booking) {
        booking.status = 'CANCELLED'
      }
      config.adapter = async () => mockResponse(booking)
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

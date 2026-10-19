export const APP_NAME = import.meta.env.VITE_APP_NAME ?? 'Vehicle Rental'

export const ROUTES = {
  LOGIN: '/login',
  REGISTER: '/register',
  DASHBOARD: '/',
  USERS: '/users',
  SEARCH: '/search',
  BOOKINGS: '/bookings',
  PROFILE: '/profile',
  PROFILE_VERIFY: '/profile/verify',
  NOT_FOUND: '/404',
} as const

export const QUERY_KEYS = {
  USERS: 'users',
  USER: 'user',
  DASHBOARD_STATS: 'dashboardStats',
  PROFILE: 'profile',
  KYC_STATUS: 'kycStatus',
  VEHICLES: 'vehicles',
  VEHICLE: 'vehicle',
  BOOKINGS: 'bookings',
} as const

export const VN_PROVINCES = [
  'An Giang', 'Ba Ria - Vung Tau', 'Bac Giang', 'Bac Kan', 'Bac Lieu',
  'Bac Ninh', 'Ben Tre', 'Binh Dinh', 'Binh Duong', 'Binh Phuoc',
  'Binh Thuan', 'Ca Mau', 'Cao Bang', 'Can Tho', 'Da Nang',
  'Dak Lak', 'Dak Nong', 'Dien Bien', 'Dong Nai', 'Dong Thap',
  'Gia Lai', 'Ha Giang', 'Ha Nam', 'Ha Noi', 'Ha Tinh',
  'Hai Duong', 'Hai Phong', 'Hau Giang', 'Hoa Binh', 'Hung Yen',
  'Khanh Hoa', 'Kien Giang', 'Kon Tum', 'Lai Chau', 'Lam Dong',
  'Lang Son', 'Lao Cai', 'Long An', 'Nam Dinh', 'Nghe An',
  'Ninh Binh', 'Ninh Thuan', 'Phu Tho', 'Phu Yen', 'Quang Binh',
  'Quang Nam', 'Quang Ngai', 'Quang Ninh', 'Quang Tri', 'Soc Trang',
  'Son La', 'Tay Ninh', 'Thai Binh', 'Thai Nguyen', 'Thanh Hoa',
  'Thua Thien Hue', 'Tien Giang', 'Ho Chi Minh City', 'Tra Vinh',
  'Tuyen Quang', 'Vinh Long', 'Vinh Phuc', 'Yen Bai',
] as const

export const POPULAR_COUNTRIES = [
  'United States',
  'South Korea',
  'Japan',
  'Australia',
  'United Kingdom',
  'France',
  'Germany',
  'Russia',
  'China',
  'Taiwan',
  'Singapore',
  'Malaysia',
  'Thailand',
  'Canada',
  'India',
  'Philippines',
  'Indonesia',
  'Vietnam',
] as const

export const DEFAULT_PAGE_SIZE = 10
export const PAGE_SIZE_OPTIONS = [10, 25, 50] as const

export const TOKEN_KEY = 'access_token'
export const REFRESH_TOKEN_KEY = 'refresh_token'

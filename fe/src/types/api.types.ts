// Generic API response wrapper from Spring Boot backend
export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
  timestamp?: string
}

// Paginated response wrapper
export interface PaginatedResponse<T> {
  content: T[]
  totalElements: number
  totalPages: number
  size: number
  number: number
  first: boolean
  last: boolean
  empty: boolean
}

// API Error response (matches Spring Boot error structure)
export interface ApiError {
  success: false
  message: string
  errors?: Record<string, string[]>
  status?: number
  timestamp?: string
}

// Paginated request params
export interface PaginationParams {
  page?: number
  size?: number
  sort?: string
  direction?: 'asc' | 'desc'
}

// Generic select option
export interface SelectOption {
  value: string
  label: string
}

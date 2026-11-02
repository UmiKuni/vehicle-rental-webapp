export type SortDirection = 'asc' | 'desc'

export interface SortConfig {
  field: string
  direction: SortDirection
}

export interface TableFilters {
  search?: string
  page: number
  size: number
  sort?: string
  direction?: SortDirection
}

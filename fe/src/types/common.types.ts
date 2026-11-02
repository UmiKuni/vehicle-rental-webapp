// Generic ID type
export type ID = number | string

// Status type for forms
export type FormMode = 'create' | 'edit' | 'view'

// Component sizes
export type Size = 'sm' | 'md' | 'lg'

// Theme type
export type Theme = 'light' | 'dark' | 'system'

// PropsWithClassName helper
export interface PropsWithClassName {
  className?: string
}

// Children prop
export interface PropsWithChildren {
  children: React.ReactNode
}

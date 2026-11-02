import { clsx } from 'clsx'
import type { ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** Merge Tailwind classes with conflict resolution */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/** Format a date string to locale display */
export function formatDate(dateString: string, format = 'dd/MM/yyyy'): string {
  try {
    // Simple format without importing date-fns to keep this utility lean
    const date = new Date(dateString)
    if (isNaN(date.getTime())) return '-'

    if (format === 'dd/MM/yyyy') {
      const d = String(date.getDate()).padStart(2, '0')
      const m = String(date.getMonth() + 1).padStart(2, '0')
      const y = date.getFullYear()
      return `${d}/${m}/${y}`
    }
    return date.toLocaleDateString('vi-VN')
  } catch {
    return '-'
  }
}

/** Truncate a string to a max length */
export function truncate(str: string, maxLength: number): string {
  if (str.length <= maxLength) return str
  return `${str.slice(0, maxLength)}...`
}

/** Get initials from a full name */
export function getInitials(fullName: string): string {
  return fullName
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2)
}

/** Check if a value is defined (not null/undefined) */
export function isDefined<T>(value: T | null | undefined): value is T {
  return value !== null && value !== undefined
}

/** Safely encode UTF-8 string to base64 without throwing Latin1 errors */
export function safeBase64Encode(str: string): string {
  const bytes = new TextEncoder().encode(str)
  let bin = ''
  for (let i = 0; i < bytes.length; i++) {
    bin += String.fromCharCode(bytes[i])
  }
  return btoa(bin)
}

/** Safely decode base64 to UTF-8 string without throwing Latin1 errors */
export function safeBase64Decode(str: string): string {
  const bin = atob(str)
  const bytes = Uint8Array.from(bin, (c) => c.charCodeAt(0))
  return new TextDecoder().decode(bytes)
}

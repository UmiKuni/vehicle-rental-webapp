import { useEffect, useRef, useState } from 'react'

/**
 * Debounce a value by the given delay (ms).
 * Used to reduce API calls while user is typing.
 */
export function useDebounce<T>(value: T, delay = 300): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value)
  const timerRef = useRef<ReturnType<typeof setTimeout>>(undefined)

  useEffect(() => {
    timerRef.current = setTimeout(() => setDebouncedValue(value), delay)
    return () => clearTimeout(timerRef.current)
  }, [value, delay])

  return debouncedValue
}

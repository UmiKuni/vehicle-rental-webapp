import { Moon, Sun } from 'lucide-react'
import { useTheme } from '@/shared/contexts/ThemeContext'

export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme()
  const isDark = resolvedTheme === 'dark'

  return (
    <button
      type="button"
      role="switch"
      aria-checked={isDark}
      aria-label={`Dark mode ${isDark ? 'on' : 'off'}`}
      title={`Switch to ${isDark ? 'light' : 'dark'} mode`}
      onClick={() => setTheme(isDark ? 'light' : 'dark')}
      className="inline-flex min-h-10 items-center gap-2 rounded-full border border-border bg-card px-3 text-xs font-semibold text-foreground shadow-sm transition hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#1a8a9d]"
    >
      {isDark ? <Moon size={15} aria-hidden="true" /> : <Sun size={15} aria-hidden="true" />}
      <span>{isDark ? 'Dark' : 'Light'}</span>
      <span aria-hidden="true" className={`relative h-5 w-9 rounded-full transition-colors ${isDark ? 'bg-[#1a8a9d]' : 'bg-muted-foreground/35'}`}>
        <span className={`absolute top-0.5 size-4 rounded-full bg-white shadow-sm transition-transform ${isDark ? 'translate-x-4' : 'translate-x-0.5'}`} />
      </span>
    </button>
  )
}

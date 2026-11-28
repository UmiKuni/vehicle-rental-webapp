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
      className="inline-flex min-h-11 items-center gap-2.5 rounded-full border px-3.5 text-sm font-bold shadow-sm transition hover:brightness-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#1a8a9d]"
      style={{
        color: isDark ? '#f8fafc' : '#0f172a',
        backgroundColor: isDark ? '#18181b' : '#ffffff',
        borderColor: isDark ? '#52525b' : '#cbd5e1',
      }}
    >
      {isDark ? <Moon size={17} className="text-sky-300" aria-hidden="true" /> : <Sun size={17} className="text-amber-600" aria-hidden="true" />}
      <span className="min-w-10 text-center">{isDark ? 'Dark' : 'Light'}</span>
      <span
        aria-hidden="true"
        className="relative h-6 w-11 shrink-0 rounded-full transition-colors"
        style={{ backgroundColor: isDark ? '#168b9a' : '#64748b' }}
      >
        <span
          className="absolute left-0.5 top-0.5 size-5 rounded-full bg-white shadow transition-transform duration-200"
          style={{ transform: `translateX(${isDark ? 20 : 0}px)` }}
        />
      </span>
    </button>
  )
}

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
      className="inline-flex min-h-11 items-center gap-2.5 rounded-full border border-slate-300 bg-white px-3.5 text-sm font-bold shadow-sm transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#1a8a9d] dark:border-zinc-600 dark:bg-zinc-900 dark:hover:bg-zinc-800"
      style={{ color: isDark ? '#f8fafc' : '#0f172a' }}
    >
      {isDark ? <Moon size={17} className="text-sky-300" aria-hidden="true" /> : <Sun size={17} className="text-amber-600" aria-hidden="true" />}
      <span className="min-w-10 text-center">{isDark ? 'Dark' : 'Light'}</span>
      <span aria-hidden="true" className={`relative h-5 w-9 rounded-full transition-colors ${isDark ? 'bg-[#1a8a9d]' : 'bg-slate-500'}`}>
        <span className={`absolute top-0.5 size-4 rounded-full bg-white shadow-sm transition-transform ${isDark ? 'translate-x-4' : 'translate-x-0.5'}`} />
      </span>
    </button>
  )
}

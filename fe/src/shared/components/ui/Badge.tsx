import type { ReactNode } from 'react'
import { cn } from '@/shared/utils'

interface BadgeProps {
  children: ReactNode
  variant?: 'default' | 'success' | 'warning' | 'destructive' | 'secondary' | 'outline' | 'neutral'
  dot?: boolean
  className?: string
}

const variantClasses: Record<NonNullable<BadgeProps['variant']>, string> = {
  default: 'bg-primary/10 text-primary border-primary/25',
  success: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/25',
  warning: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/25',
  destructive: 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/25',
  secondary: 'bg-secondary text-secondary-foreground border-border/80',
  outline: 'border-border text-foreground bg-transparent',
  neutral: 'bg-muted/60 text-muted-foreground border-border/60',
}

export function Badge({ children, variant = 'default', dot, className }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md border px-2 py-0.5 text-[11px] font-medium tracking-tight transition-colors',
        variantClasses[variant],
        className
      )}
    >
      {dot && <span className="size-1.5 rounded-full bg-current opacity-80 shrink-0" />}
      {children}
    </span>
  )
}

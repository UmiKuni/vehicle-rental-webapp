import type { ElementType } from 'react'
import { TrendingUp, TrendingDown } from 'lucide-react'
import { cn } from '@/shared/utils'

interface FleetMetricCardProps {
  label: string
  value: string
  subValue?: string
  trend: number
  trendLabel?: string
  icon: ElementType
  statusColor?: 'emerald' | 'blue' | 'amber' | 'rose'
}

export function FleetMetricCard({
  label,
  value,
  subValue,
  trend,
  trendLabel = 'vs last month',
  icon: Icon,
  statusColor = 'blue',
}: FleetMetricCardProps) {
  const isPositive = trend >= 0

  const colorStyles = {
    blue: 'text-blue-500 bg-blue-500/10 border-blue-500/20',
    emerald: 'text-emerald-500 bg-emerald-500/10 border-emerald-500/20',
    amber: 'text-amber-500 bg-amber-500/10 border-amber-500/20',
    rose: 'text-rose-500 bg-rose-500/10 border-rose-500/20',
  }[statusColor]

  return (
    <div className="rounded-xl border border-border bg-card p-4.5 shadow-2xs hover:border-border/80 transition-all">
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium text-muted-foreground">{label}</span>
        <div className={cn('flex h-8 w-8 items-center justify-center rounded-lg border', colorStyles)}>
          <Icon className="size-4" />
        </div>
      </div>

      <div className="mt-3 flex items-baseline gap-2">
        <span className="text-2xl font-bold font-mono tracking-tight text-foreground">{value}</span>
        {subValue && <span className="text-xs text-muted-foreground font-mono">({subValue})</span>}
      </div>

      <div className="mt-2.5 flex items-center gap-1.5 text-[11px]">
        <span
          className={cn(
            'inline-flex items-center gap-0.5 font-semibold font-mono px-1.5 py-0.5 rounded',
            isPositive
              ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
              : 'bg-rose-500/10 text-rose-600 dark:text-rose-400'
          )}
        >
          {isPositive ? <TrendingUp className="size-3" /> : <TrendingDown className="size-3" />}
          {isPositive ? '+' : ''}{trend}%
        </span>
        <span className="text-muted-foreground">{trendLabel}</span>
      </div>
    </div>
  )
}

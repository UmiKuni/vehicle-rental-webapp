import { useState } from 'react'
import { BarChart2, TrendingUp } from 'lucide-react'
import { cn } from '@/shared/utils'

interface DayStat {
  day: string
  date: string
  revenue: number
  bookings: number
  utilization: number
}

const WEEKLY_STATS: DayStat[] = [
  { day: 'Mon', date: '10/07', revenue: 18_500_000, bookings: 14, utilization: 72 },
  { day: 'Tue', date: '10/08', revenue: 22_000_000, bookings: 17, utilization: 78 },
  { day: 'Wed', date: '10/09', revenue: 19_800_000, bookings: 15, utilization: 75 },
  { day: 'Thu', date: '10/10', revenue: 28_400_000, bookings: 21, utilization: 86 },
  { day: 'Fri', date: '10/11', revenue: 34_600_000, bookings: 26, utilization: 92 },
  { day: 'Sat', date: '10/12', revenue: 42_100_000, bookings: 31, utilization: 98 },
  { day: 'Sun', date: '10/13', revenue: 38_000_000, bookings: 29, utilization: 94 },
]

export function RevenueTrendChart() {
  const [activeIdx, setActiveIdx] = useState<number | null>(6)
  const maxRevenue = Math.max(...WEEKLY_STATS.map((s) => s.revenue))
  const selected = activeIdx !== null ? WEEKLY_STATS[activeIdx] : WEEKLY_STATS[6]

  return (
    <div className="rounded-xl border border-border bg-card p-5 shadow-2xs">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-border/60 pb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-semibold text-foreground">Revenue & Booking Trends</h3>
            <span className="inline-flex items-center gap-1 rounded bg-blue-500/10 px-1.5 py-0.5 text-[10px] font-medium font-mono text-blue-600 dark:text-blue-400">
              <TrendingUp className="size-3" /> +18.4%
            </span>
          </div>
          <p className="mt-0.5 text-xs text-muted-foreground">Daily revenue breakdown across the week</p>
        </div>

        {/* Selected Data Snapshot */}
        <div className="flex items-center gap-4 bg-muted/40 rounded-lg px-3 py-1.5 border border-border/40">
          <div>
            <p className="text-[10px] uppercase font-mono text-muted-foreground">{selected.day} ({selected.date})</p>
            <p className="text-sm font-bold font-mono text-foreground">
              {new Intl.NumberFormat('en-US', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(selected.revenue)}
            </p>
          </div>
          <div className="border-l border-border/60 pl-3">
            <p className="text-[10px] uppercase font-mono text-muted-foreground">Utilization</p>
            <p className="text-sm font-bold font-mono text-emerald-500">{selected.utilization}%</p>
          </div>
        </div>
      </div>

      {/* Chart Bars */}
      <div className="mt-6 flex h-48 items-end gap-3 sm:gap-6 pt-4">
        {WEEKLY_STATS.map((stat, i) => {
          const heightPercent = Math.round((stat.revenue / maxRevenue) * 100)
          const isSelected = activeIdx === i

          return (
            <div
              key={stat.day}
              onMouseEnter={() => setActiveIdx(i)}
              className="group relative flex flex-1 flex-col items-center h-full justify-end cursor-pointer"
            >
              {/* Bar */}
              <div className="w-full max-w-[42px] flex flex-col justify-end h-full">
                <div
                  style={{ height: `${heightPercent}%` }}
                  className={cn(
                    'w-full rounded-t-md transition-all duration-200',
                    isSelected
                      ? 'bg-primary shadow-xs'
                      : 'bg-muted-foreground/20 hover:bg-muted-foreground/35'
                  )}
                />
              </div>

              {/* Label */}
              <div className="mt-2 text-center">
                <p className={cn('text-[11px] font-medium transition-colors', isSelected ? 'text-foreground font-semibold' : 'text-muted-foreground')}>
                  {stat.day}
                </p>
                <p className="text-[9px] font-mono text-muted-foreground">{stat.date}</p>
              </div>
            </div>
          )
        })}
      </div>

      {/* Footer Info */}
      <div className="mt-4 flex items-center justify-between border-t border-border/40 pt-3 text-[11px] text-muted-foreground">
        <div className="flex items-center gap-2">
          <BarChart2 className="size-3.5" />
          <span>Total Weekly Revenue: <strong className="font-mono text-foreground">203,400,000 ₫</strong></span>
        </div>
        <span>149 completed rentals</span>
      </div>
    </div>
  )
}

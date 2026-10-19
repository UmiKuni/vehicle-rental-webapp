import { ShieldCheck, ShieldX, Clock, ShieldAlert } from 'lucide-react'
import { cn } from '@/shared/utils'
import type { KycStatus } from '@/types/kyc.types'

interface Props {
  status: KycStatus
  size?: 'sm' | 'md' | 'lg'
  showLabel?: boolean
}

const CONFIG = {
  VERIFIED: {
    icon: ShieldCheck,
    label: 'Verified',
    className: 'bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-400 dark:border-emerald-800',
    dot: 'bg-emerald-500',
  },
  PENDING: {
    icon: Clock,
    label: 'In Review',
    className: 'bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-950/40 dark:text-amber-400 dark:border-amber-800',
    dot: 'bg-amber-500',
  },
  UNVERIFIED: {
    icon: ShieldAlert,
    label: 'Unverified',
    className: 'bg-zinc-50 text-zinc-600 border-zinc-200 dark:bg-zinc-800/40 dark:text-zinc-400 dark:border-zinc-700',
    dot: 'bg-zinc-400',
  },
  REJECTED: {
    icon: ShieldX,
    label: 'Rejected',
    className: 'bg-red-50 text-red-700 border-red-200 dark:bg-red-950/40 dark:text-red-400 dark:border-red-800',
    dot: 'bg-red-500',
  },
} satisfies Record<KycStatus, { icon: typeof ShieldCheck; label: string; className: string; dot: string }>

const sizeMap = {
  sm: { badge: 'px-2 py-0.5 text-[10px] gap-1', icon: 'size-2.5' },
  md: { badge: 'px-2.5 py-1 text-xs gap-1.5', icon: 'size-3.5' },
  lg: { badge: 'px-3 py-1.5 text-sm gap-2', icon: 'size-4' },
}

export function VerificationBadge({ status, size = 'md', showLabel = true }: Props) {
  const cfg = CONFIG[status]
  const s = sizeMap[size]
  const Icon = cfg.icon

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full border font-semibold',
        cfg.className,
        s.badge
      )}
    >
      <Icon className={cn(s.icon, 'shrink-0')} />
      {showLabel && <span>{cfg.label}</span>}
    </span>
  )
}

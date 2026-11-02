import { Sparkles, ArrowLeft } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useAuth } from '@/shared/contexts/AuthContext'
import { ADMIN_ROLES } from '@/types/auth.types'
import { ROUTES } from '@/shared/constants'
import { useSetMockKycStatus } from '../hooks/useProfile'
import type { KycStatus } from '@/types/kyc.types'
import { cn } from '@/shared/utils'

interface KycDevTesterProps {
  currentStatus: KycStatus
}

const STATES: { status: KycStatus; label: string; activeClass: string }[] = [
  { status: 'UNVERIFIED', label: 'Unverified', activeClass: 'bg-zinc-700 text-white' },
  { status: 'PENDING',    label: 'Pending',    activeClass: 'bg-amber-500 text-white' },
  { status: 'VERIFIED',   label: 'Verified',   activeClass: 'bg-emerald-500 text-white' },
  { status: 'REJECTED',   label: 'Rejected',   activeClass: 'bg-red-500 text-white' },
]

export function KycDevTester({ currentStatus }: KycDevTesterProps) {
  const { user } = useAuth()
  const { mutate: setStatus, isPending } = useSetMockKycStatus()
  const isAdmin = user && ADMIN_ROLES.includes(user.role)

  return (
    <div className="mx-4 mb-2 rounded-2xl border border-dashed border-[#1a8a9d]/40 bg-[#1a8a9d]/5 p-3">
      <div className="flex items-center justify-between mb-2">
        <span className="flex items-center gap-1.5 text-[11px] font-semibold text-[#1a8a9d]">
          <Sparkles className="size-3.5" />
          Quick test 4 wireframe states:
        </span>
        {isAdmin && (
          <Link
            to={ROUTES.DASHBOARD}
            className="flex items-center gap-1 text-[11px] font-semibold text-muted-foreground hover:text-foreground transition-colors"
          >
            <ArrowLeft className="size-3" />
            Dashboard
          </Link>
        )}
      </div>

      <div className="grid grid-cols-4 gap-1.5">
        {STATES.map(({ status, label, activeClass }) => {
          const isActive = currentStatus === status
          return (
            <button
              key={status}
              type="button"
              disabled={isPending}
              onClick={() => setStatus(status)}
              className={cn(
                'min-h-[34px] rounded-lg px-1.5 py-1 text-[10px] font-medium transition-all text-center',
                isActive
                  ? cn('font-bold shadow-xs', activeClass)
                  : 'bg-background/80 text-muted-foreground hover:bg-background hover:text-foreground border border-border/60'
              )}
            >
              {label}
            </button>
          )
        })}
      </div>
    </div>
  )
}

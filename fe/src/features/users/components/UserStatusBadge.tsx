import type { UserStatus } from '@/types/user.types'
import { Badge } from '@/shared/components/ui/Badge'

const statusConfig: Record<UserStatus, { label: string; variant: 'success' | 'neutral' | 'destructive' }> = {
  ACTIVE: { label: 'Active', variant: 'success' },
  INACTIVE: { label: 'Inactive', variant: 'neutral' },
  BANNED: { label: 'Banned', variant: 'destructive' },
}

export function UserStatusBadge({ status }: { status: UserStatus }) {
  const { label, variant } = statusConfig[status] ?? statusConfig.ACTIVE
  return <Badge variant={variant} dot>{label}</Badge>
}

import type { UserRole } from '@/types/auth.types'
import { Badge } from '@/shared/components/ui/Badge'

const roleConfig: Record<UserRole, { label: string; variant: 'default' | 'outline' | 'secondary' }> = {
  ADMIN: { label: 'Admin', variant: 'default' },
  MANAGER: { label: 'Quản lý xe', variant: 'outline' },
  STAFF: { label: 'Điều phối viên', variant: 'secondary' },
}

export function UserRoleBadge({ role }: { role: UserRole }) {
  const { label, variant } = roleConfig[role] ?? roleConfig.STAFF
  return <Badge variant={variant}>{label}</Badge>
}

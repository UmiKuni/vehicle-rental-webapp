import { Shield, Users as UsersIcon } from 'lucide-react'
import { UserTable } from '../components/UserTable'

export default function UsersPage() {
  return (
    <div className="space-y-6">
      {/* Directory Header */}
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between border-b border-border/60 pb-5">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold tracking-tight text-foreground sm:text-2xl">
              User Management & Permissions
            </h1>
            <span className="rounded-md border border-border bg-muted/60 px-2 py-0.5 text-[11px] font-mono font-medium text-foreground">
              RBAC v2
            </span>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            Directory of dispatchers, fleet station managers, and system administrators
          </p>
        </div>

        <div className="flex items-center gap-3 text-xs text-muted-foreground">
          <div className="flex items-center gap-1.5 rounded-lg border border-border bg-card px-3 py-1.5 shadow-2xs">
            <UsersIcon className="size-3.5 text-primary" />
            <span>Total: <strong className="font-mono text-foreground">35</strong> accounts</span>
          </div>
          <div className="flex items-center gap-1.5 rounded-lg border border-border bg-card px-3 py-1.5 shadow-2xs">
            <Shield className="size-3.5 text-emerald-500" />
            <span>Auth: <strong className="text-foreground">JWT Multi-role</strong></span>
          </div>
        </div>
      </div>

      <UserTable />
    </div>
  )
}

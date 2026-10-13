import { useState } from 'react'
import { Pencil, Trash2, Search, UserPlus, X } from 'lucide-react'
import { useUsers } from '../hooks/useUsers'
import type { UserResponse } from '@/types/user.types'
import { UserStatusBadge } from './UserStatusBadge'
import { UserRoleBadge } from './UserRoleBadge'
import { CreateUserDialog } from './CreateUserDialog'
import { EditUserDialog } from './EditUserDialog'
import { DeleteUserDialog } from './DeleteUserDialog'
import { TableSkeleton } from '@/shared/components/common/Skeleton'
import { ErrorState, EmptyState } from '@/shared/components/common/StateViews'
import { Pagination } from '@/shared/components/common/Pagination'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { formatDate, getInitials, cn } from '@/shared/utils'
import { DEFAULT_PAGE_SIZE } from '@/shared/constants'
import { useDebounce } from '@/shared/hooks/useDebounce'

export function UserTable() {
  const [page, setPage] = useState(0)
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState<'ALL' | 'ADMIN' | 'MANAGER' | 'STAFF'>('ALL')
  const [showCreate, setShowCreate] = useState(false)
  const [editUser, setEditUser] = useState<UserResponse | null>(null)
  const [deleteUser, setDeleteUser] = useState<UserResponse | null>(null)

  const debouncedSearch = useDebounce(search, 350)

  const { data, isLoading, isError, refetch } = useUsers({
    page,
    size: DEFAULT_PAGE_SIZE,
    search: debouncedSearch || undefined,
  })

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearch(e.target.value)
    setPage(0)
  }

  // Filter content by role if selected
  const displayContent = data?.content.filter(
    (u) => roleFilter === 'ALL' || u.role === roleFilter
  )

  return (
    <div className="space-y-4">
      {/* Search & Actions Toolbar */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-1 items-center gap-2 max-w-md">
          <div className="relative w-full">
            <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="user-search"
              placeholder="Tìm theo tên, username, email..."
              value={search}
              onChange={handleSearchChange}
              className="pl-9 pr-8 h-9 text-xs"
            />
            {search && (
              <button
                type="button"
                onClick={() => setSearch('')}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground p-0.5"
              >
                <X className="size-3.5" />
              </button>
            )}
          </div>
        </div>

        {/* Role Quick Chips */}
        <div className="flex items-center gap-1.5 overflow-x-auto">
          {(['ALL', 'ADMIN', 'MANAGER', 'STAFF'] as const).map((r) => (
            <button
              key={r}
              type="button"
              onClick={() => setRoleFilter(r)}
              className={cn(
                'rounded-md px-2.5 py-1 text-xs font-medium transition-colors border',
                roleFilter === r
                  ? 'bg-foreground text-background border-foreground font-semibold'
                  : 'bg-muted/40 text-muted-foreground border-border hover:bg-accent hover:text-foreground'
              )}
            >
              {r === 'ALL' ? 'Tất cả' : r}
            </button>
          ))}
          <Button id="btn-create-user" size="sm" onClick={() => setShowCreate(true)} className="ml-2 gap-1.5 h-8">
            <UserPlus className="size-3.5" />
            Thêm tài khoản
          </Button>
        </div>
      </div>

      {/* Main Table Container */}
      <div className="rounded-xl border border-border bg-card shadow-2xs overflow-hidden">
        {isLoading ? (
          <div className="p-4">
            <TableSkeleton rows={DEFAULT_PAGE_SIZE} />
          </div>
        ) : isError ? (
          <ErrorState onRetry={refetch} />
        ) : !data || !displayContent || displayContent.length === 0 ? (
          <EmptyState
            title="Không tìm thấy tài khoản"
            description={search ? 'Thử tìm với từ khóa khác hoặc xóa bộ lọc.' : 'Thêm tài khoản đầu tiên vào hệ thống.'}
            action={
              <Button size="sm" onClick={() => setShowCreate(true)}>
                <UserPlus className="size-4" />
                Thêm tài khoản mới
              </Button>
            }
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-border/60 bg-muted/30 text-muted-foreground font-medium">
                    <th className="px-4 py-2.5 text-left font-semibold uppercase tracking-wider text-[11px]">Họ tên & Định danh</th>
                    <th className="px-4 py-2.5 text-left font-semibold uppercase tracking-wider text-[11px]">Email liên hệ</th>
                    <th className="px-4 py-2.5 text-left font-semibold uppercase tracking-wider text-[11px]">Điện thoại</th>
                    <th className="px-4 py-2.5 text-left font-semibold uppercase tracking-wider text-[11px]">Vai trò</th>
                    <th className="px-4 py-2.5 text-left font-semibold uppercase tracking-wider text-[11px]">Trạng thái</th>
                    <th className="px-4 py-2.5 text-left font-semibold uppercase tracking-wider text-[11px]">Ngày tạo</th>
                    <th className="px-4 py-2.5 text-right font-semibold uppercase tracking-wider text-[11px]">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/40">
                  {displayContent.map((user) => (
                    <tr key={user.id} className="hover:bg-muted/20 transition-colors">
                      <td className="px-4 py-2.5">
                        <div className="flex items-center gap-2.5">
                          <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md border border-border bg-muted/60 text-[11px] font-bold font-mono text-foreground">
                            {getInitials(user.fullName)}
                          </div>
                          <div className="min-w-0">
                            <p className="font-semibold text-foreground truncate">{user.fullName}</p>
                            <p className="font-mono text-[10px] text-muted-foreground">@{user.username}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-2.5 font-mono text-muted-foreground">{user.email}</td>
                      <td className="px-4 py-2.5 font-mono text-foreground">{user.phone ?? '—'}</td>
                      <td className="px-4 py-2.5">
                        <UserRoleBadge role={user.role} />
                      </td>
                      <td className="px-4 py-2.5">
                        <UserStatusBadge status={user.status} />
                      </td>
                      <td className="px-4 py-2.5 font-mono text-muted-foreground tabular-nums">
                        {formatDate(user.createdAt)}
                      </td>
                      <td className="px-4 py-2.5 text-right">
                        <div className="inline-flex items-center gap-1">
                          <button
                            onClick={() => setEditUser(user)}
                            className="flex items-center justify-center min-w-[36px] min-h-[36px] rounded-md text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
                            title="Sửa thông tin"
                          >
                            <Pencil className="size-3.5" />
                          </button>
                          <button
                            onClick={() => setDeleteUser(user)}
                            className="flex items-center justify-center min-w-[36px] min-h-[36px] rounded-md text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors"
                            title="Xóa tài khoản"
                          >
                            <Trash2 className="size-3.5" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            <div className="border-t border-border/60 px-4">
              <Pagination
                page={data.number}
                totalPages={data.totalPages}
                totalElements={data.totalElements}
                size={data.size}
                onPageChange={setPage}
              />
            </div>
          </>
        )}
      </div>

      {/* Dialogs */}
      <CreateUserDialog open={showCreate} onOpenChange={setShowCreate} />
      <EditUserDialog user={editUser} open={!!editUser} onOpenChange={(v) => !v && setEditUser(null)} />
      <DeleteUserDialog user={deleteUser} open={!!deleteUser} onOpenChange={(v) => !v && setDeleteUser(null)} />
    </div>
  )
}

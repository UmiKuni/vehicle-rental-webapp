import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertCircle,
  Ban,
  Check,
  RefreshCw,
  Search,
  ShieldCheck,
  UserRoundCheck,
  UsersRound,
  X,
} from 'lucide-react'
import { toast } from 'sonner'
import apiClient from '@/shared/lib/axios'
import { useAuth } from '@/shared/contexts/AuthContext'
import { Button } from '@/shared/components/ui/Button'

interface AdminUser {
  userId: string
  fullName: string
  email: string
  phone: string
  role: string
  status: 'Active' | 'Banned'
}
interface AccessChange {
  user: AdminUser
  action: 'ban' | 'unban'
}
type StatusFilter = 'all' | 'active' | 'banned'

export default function AdminUsersPage() {
  const { user: currentUser } = useAuth()
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all')
  const [confirmation, setConfirmation] = useState<AccessChange | null>(null)
  const users = useQuery({
    queryKey: ['admin', 'users'],
    queryFn: async () => (await apiClient.get<AdminUser[]>('/users')).data,
  })
  const mutation = useMutation({
    mutationFn: async ({ user, action }: AccessChange) =>
      apiClient.put(`/users/${user.userId}/${action}`),
    onSuccess: async (_data, change) => {
      toast.success(
        change.action === 'ban' ? 'Account access suspended' : 'Account access restored'
      )
      setConfirmation(null)
      await queryClient.invalidateQueries({ queryKey: ['admin', 'users'] })
    },
    onError: () => toast.error('Could not update account access. Check your admin permissions.'),
  })
  const accounts = users.data ?? []
  const activeCount = accounts.filter((account) => account.status === 'Active').length
  const bannedCount = accounts.length - activeCount
  const rows = useMemo(
    () =>
      accounts.filter((account) => {
        const matchesSearch =
          `${account.fullName} ${account.email} ${account.phone} ${account.role} ${account.status} ${account.userId}`
            .toLowerCase()
            .includes(search.toLowerCase())
        const matchesStatus =
          statusFilter === 'all' ||
          (statusFilter === 'active' ? account.status === 'Active' : account.status === 'Banned')
        return matchesSearch && matchesStatus
      }),
    [accounts, search, statusFilter]
  )

  return (
    <div className="space-y-5">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="mb-1.5 text-[10px] font-extrabold tracking-[.14em] text-[#178b8d]">
            ACCOUNT OPERATIONS
          </p>
          <h1 className="m-0 text-[27px] font-extrabold leading-tight tracking-[-.04em] text-[#172725] sm:text-[30px]">
            User access
          </h1>
          <p className="mb-0 mt-1.5 text-sm font-medium text-[#65746d]">
            Review accounts and manage platform access.
          </p>
        </div>
        <div className="inline-flex w-fit items-center gap-2 rounded-lg border border-[#dce8df] bg-white px-3 py-2 text-xs font-bold text-[#52655a]">
          <ShieldCheck size={15} className="text-[#39734f]" /> Admin-only access
        </div>
      </div>

      <section className="overflow-hidden rounded-xl border border-[#dce5df] bg-white shadow-[0_2px_10px_rgba(28,51,39,.035)]">
        <div className="flex flex-col gap-3 border-b border-[#e8eeea] px-4 py-3 sm:px-5 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-3">
            <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-[#edf4ef] text-[#39734f]">
              <UsersRound size={19} />
            </span>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="m-0 text-base font-bold tracking-[-.015em] text-[#20312c]">
                  Platform accounts
                </h2>
                <span className="rounded-md bg-[#eef3ef] px-2 py-0.5 font-mono text-xs font-bold tabular-nums text-[#53645a]">
                  {users.isError ? '—' : users.isPending ? '…' : accounts.length}
                </span>
              </div>
              <p className="mb-0 mt-0.5 text-xs font-medium text-[#77847b]">
                Search accounts and manage access
              </p>
            </div>
          </div>
          <label className="relative block w-full md:max-w-[320px]">
            <Search
              size={16}
              className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#829087]"
            />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search name, email, phone or ID"
              className="h-9 w-full rounded-lg border border-[#dce5df] bg-white pl-9 pr-3 text-sm font-medium text-[#263733] outline-none transition placeholder:font-normal placeholder:text-[#99a39c] focus:border-[#78a99a] focus:ring-2 focus:ring-[#78a99a]/15"
            />
          </label>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[#e8eeea] bg-[#fbfcfb] px-4 py-2 sm:px-5">
          <div
            className="flex items-center gap-1"
            role="tablist"
            aria-label="Filter accounts by status"
          >
            {(
              [
                {
                  id: 'all',
                  label: 'All accounts',
                  count: users.isError ? undefined : users.isPending ? undefined : accounts.length,
                },
                {
                  id: 'active',
                  label: 'Active',
                  count: users.isError ? undefined : users.isPending ? undefined : activeCount,
                },
                {
                  id: 'banned',
                  label: 'Suspended',
                  count: users.isError ? undefined : users.isPending ? undefined : bannedCount,
                },
              ] as const
            ).map((filter) => (
              <button
                key={filter.id}
                role="tab"
                aria-selected={statusFilter === filter.id}
                onClick={() => setStatusFilter(filter.id)}
                className={`inline-flex h-8 items-center gap-1.5 rounded-md px-2.5 text-xs font-bold transition ${statusFilter === filter.id ? 'bg-white text-[#245c47] shadow-sm ring-1 ring-[#dfe8e1]' : 'text-[#718078] hover:bg-white hover:text-[#34483b]'}`}
              >
                {filter.label}
                {filter.count !== undefined && (
                  <span
                    className={`font-mono text-[11px] tabular-nums ${statusFilter === filter.id ? 'text-[#39734f]' : 'text-[#89948c]'}`}
                  >
                    {filter.count}
                  </span>
                )}
              </button>
            ))}
          </div>
          <span className="text-xs font-medium text-[#829087]">
            {users.isError
              ? 'Account data unavailable'
              : users.isPending
                ? 'Loading accounts…'
                : `${rows.length} shown`}
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[850px] text-left text-sm">
            <thead className="bg-[#f6f8f6] text-[11px] font-extrabold uppercase tracking-[.09em] text-[#65736a]">
              <tr>
                <th className="w-[40%] px-5 py-2.5">Account</th>
                <th className="w-[19%] px-4 py-2.5">Phone</th>
                <th className="w-[13%] px-4 py-2.5">Role</th>
                <th className="w-[14%] px-4 py-2.5">Status</th>
                <th className="w-[14%] px-5 py-2.5 text-right">Access</th>
              </tr>
            </thead>
            <tbody>
              {users.isPending ? (
                <tr>
                  <td colSpan={5} className="px-5 py-10">
                    <div className="space-y-3">
                      {[0, 1, 2].map((index) => (
                        <div key={index} className="h-11 animate-pulse rounded-md bg-[#f2f5f2]" />
                      ))}
                    </div>
                  </td>
                </tr>
              ) : users.isError ? (
                <tr>
                  <td colSpan={5} className="px-5 py-0">
                    <div className="flex min-h-[220px] flex-col items-center justify-center px-6 py-8 text-center">
                      <span className="grid size-11 place-items-center rounded-xl border border-[#e5e9e5] bg-[#f7f8f6] text-[#78847a]">
                        <AlertCircle size={20} />
                      </span>
                      <h3 className="mb-0 mt-3 text-sm font-bold text-[#29382f]">
                        Account list is temporarily unavailable
                      </h3>
                      <p className="mb-4 mt-1 max-w-md text-xs leading-relaxed text-[#7c887f]">
                        We couldn’t get account data from the users service. Check the API
                        connection, then try again.
                      </p>
                      <Button
                        variant="outline"
                        size="sm"
                        className="font-bold text-[#40564a]"
                        onClick={() => void users.refetch()}
                      >
                        <RefreshCw size={14} /> Retry
                      </Button>
                    </div>
                  </td>
                </tr>
              ) : rows.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-5 py-0">
                    <div className="flex min-h-[200px] flex-col items-center justify-center px-6 py-8 text-center">
                      <span className="grid size-11 place-items-center rounded-xl bg-[#f1f4f1] text-[#78847a]">
                        <Search size={19} />
                      </span>
                      <h3 className="mb-0 mt-3 text-sm font-bold text-[#29382f]">
                        {search || statusFilter !== 'all'
                          ? 'No matching accounts'
                          : 'No accounts yet'}
                      </h3>
                      <p className="mb-0 mt-1 max-w-sm text-xs leading-relaxed text-[#7c887f]">
                        {search || statusFilter !== 'all'
                          ? 'Change the search or status filter and try again.'
                          : 'Accounts will appear here when they are available.'}
                      </p>
                    </div>
                  </td>
                </tr>
              ) : (
                rows.map((account) => {
                  const isSelf =
                    account.userId.toLowerCase() === currentUser?.id.toString().toLowerCase()
                  const isBanned = account.status === 'Banned'
                  return (
                    <tr
                      key={account.userId}
                      className="border-t border-[#edf0ed] text-[#344239] transition-colors hover:bg-[#fafcf9]"
                    >
                      <td className="px-5 py-2.5">
                        <div className="flex min-w-0 items-center gap-2.5">
                          <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-[#f0f3f0] text-xs font-extrabold text-[#52645a]">
                            {(account.fullName || account.email).slice(0, 1).toUpperCase()}
                          </span>
                          <div className="min-w-0">
                            <p className="m-0 truncate text-sm font-bold text-[#26362d]">
                              {account.fullName || 'Name not provided'}
                            </p>
                            <p
                              className="mb-0 mt-0.5 truncate text-xs font-medium text-[#6f7d74]"
                              title={`${account.email} · ${account.userId}`}
                            >
                              {account.email}
                            </p>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-2.5 font-mono text-[13px] font-semibold tabular-nums text-[#4c5d53]">
                        {account.phone || '—'}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="inline-flex rounded-md border border-[#e5eae5] bg-[#f8faf8] px-2 py-0.5 text-[11px] font-bold text-[#53635a]">
                          {account.role}
                        </span>
                      </td>
                      <td className="px-4 py-2.5">
                        <span
                          className={`inline-flex items-center gap-1.5 rounded-md border px-2 py-0.5 text-[11px] font-bold ${isBanned ? 'border-rose-200 bg-rose-50 text-rose-700' : 'border-emerald-200 bg-emerald-50 text-emerald-700'}`}
                        >
                          <span
                            className={`size-1.5 rounded-full ${isBanned ? 'bg-rose-600' : 'bg-emerald-600'}`}
                          />
                          {isBanned ? 'Suspended' : 'Active'}
                        </span>
                      </td>
                      <td className="px-5 py-2.5 text-right">
                        {isSelf ? (
                          <span className="text-[11px] font-semibold text-[#859188]">
                            Current account
                          </span>
                        ) : (
                          <Button
                            size="sm"
                            variant={isBanned ? 'outline' : 'destructive'}
                            className="h-8 px-2.5 text-xs font-bold"
                            disabled={mutation.isPending}
                            onClick={() =>
                              setConfirmation({ user: account, action: isBanned ? 'unban' : 'ban' })
                            }
                          >
                            {isBanned ? (
                              <>
                                <UserRoundCheck size={14} /> Restore
                              </>
                            ) : (
                              <>
                                <Ban size={14} /> Suspend
                              </>
                            )}
                          </Button>
                        )}
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>

        <div className="flex flex-col gap-2 border-t border-[#e8eeea] bg-[#fbfcfb] px-4 py-2 text-[11px] text-[#718078] sm:flex-row sm:items-center sm:justify-between sm:px-5">
          <span className="inline-flex items-center gap-2">
            <ShieldCheck size={13} className="text-[#62806b]" /> Your own signed-in account cannot
            be suspended.
          </span>
          <span>Account changes take effect immediately.</span>
        </div>
      </section>

      {confirmation && (
        <div
          className="fixed inset-0 z-50 grid place-items-center bg-[#14251c80] p-4"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) setConfirmation(null)
          }}
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="user-confirm-title"
            className="relative w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl"
          >
            <button
              onClick={() => setConfirmation(null)}
              aria-label="Close"
              className="absolute right-4 top-4 grid size-8 place-items-center rounded-lg text-[#7a867d] hover:bg-[#f2f5f2]"
            >
              <X size={17} />
            </button>
            <p className="mb-2 text-[10px] font-bold tracking-[.14em] text-[#849087]">
              CONFIRM ACCOUNT CHANGE
            </p>
            <h2 id="user-confirm-title" className="m-0 text-xl font-bold text-[#20312c]">
              {confirmation.action === 'ban' ? 'Suspend this account?' : 'Restore this account?'}
            </h2>
            <p className="mt-2 text-sm font-medium text-[#64736f]">
              {confirmation.user.fullName || confirmation.user.email} · {confirmation.user.email}
            </p>
            <code className="block truncate rounded-lg bg-[#f4f6f4] p-3 text-xs font-semibold text-[#43534a]">
              {confirmation.user.userId}
            </code>
            {mutation.isError && (
              <p role="alert" className="mt-3 text-sm font-medium text-red-700">
                The API could not complete this request.
              </p>
            )}
            <div className="mt-5 flex justify-end gap-2">
              <Button variant="outline" onClick={() => setConfirmation(null)}>
                Cancel
              </Button>
              <Button
                variant={confirmation.action === 'ban' ? 'destructive' : 'default'}
                loading={mutation.isPending}
                onClick={() => mutation.mutate(confirmation)}
              >
                {confirmation.action === 'ban' ? (
                  <>
                    <Ban size={15} /> Suspend account
                  </>
                ) : (
                  <>
                    <Check size={15} /> Restore account
                  </>
                )}
              </Button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}

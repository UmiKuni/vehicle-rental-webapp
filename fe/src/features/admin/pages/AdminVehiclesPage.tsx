import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, EyeOff, RefreshCw, Search, X } from 'lucide-react'
import { toast } from 'sonner'
import { z } from 'zod'
import apiClient from '@/shared/lib/axios'
import { Badge } from '@/shared/components/ui/Badge'
import { Button } from '@/shared/components/ui/Button'
import { ErrorState } from '@/shared/components/common/StateViews'

interface FleetVehicle {
  vehicleId: string
  ownerId: string
  plateNumber: string
  brandName: string
  modelName: string
  status: string
  transmission: string
  seats: number
  address: string
}
type ReviewAction = 'approve' | 'reject' | 'unpublish' | 'hide' | 'show'

const statusVariant = (status: string): 'success' | 'warning' | 'neutral' =>
  status === 'Published' ? 'success' : status === 'PendingApproval' ? 'warning' : 'neutral'

export default function AdminVehiclesPage() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<FleetVehicle | null>(null)
  const [reason, setReason] = useState('')
  const [lookupType, setLookupType] = useState<'published' | 'vehicle' | 'owner'>('published')
  const [lookup, setLookup] = useState('')
  const [scope, setScope] = useState<{ type: 'published' | 'vehicle' | 'owner'; id: string }>({
    type: 'published',
    id: '',
  })
  const vehicles = useQuery({
    queryKey: ['admin', 'fleet', scope],
    queryFn: async () => {
      if (scope.type === 'vehicle')
        return [(await apiClient.get<FleetVehicle>(`/fleet/vehicles/${scope.id}`)).data]
      return (
        await apiClient.get<FleetVehicle[]>(
          scope.type === 'owner' ? `/fleet/owners/${scope.id}/vehicles` : '/fleet/vehicles'
        )
      ).data
    },
  })
  const mutation = useMutation({
    mutationFn: async ({
      vehicle,
      action,
      note,
    }: {
      vehicle: FleetVehicle
      action: ReviewAction
      note?: string
    }) => {
      const body = action === 'reject' || action === 'unpublish' ? { reason: note } : undefined
      await apiClient.post(`/fleet/vehicles/${vehicle.vehicleId}/${action}`, body)
    },
    onSuccess: async () => {
      toast.success('Vehicle status updated')
      setSelected(null)
      setReason('')
      await queryClient.invalidateQueries({ queryKey: ['admin', 'fleet'] })
    },
    onError: () =>
      toast.error('Could not update this vehicle. Check the API response and permissions.'),
  })
  const rows = (vehicles.data ?? []).filter((vehicle) =>
    `${vehicle.plateNumber} ${vehicle.brandName} ${vehicle.modelName} ${vehicle.status} ${vehicle.vehicleId}`
      .toLowerCase()
      .includes(search.toLowerCase())
  )

  if (vehicles.isError)
    return (
      <ErrorState
        message="Could not load the fleet from the API."
        onRetry={() => void vehicles.refetch()}
      />
    )
  return (
    <div className="space-y-4">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
        <div>
          <p className="mb-1.5 text-[10px] font-extrabold tracking-[.14em] text-[#178b8d]">
            FLEET OPERATIONS
          </p>
          <h1 className="m-0 text-[27px] font-extrabold leading-tight tracking-[-.04em] text-[#172725] sm:text-[30px]">
            Vehicle management
          </h1>
          <p className="mb-0 mt-1.5 text-[14px] font-semibold text-[#64736f]">
            Review listings and control what customers can book.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => void vehicles.refetch()}
          loading={vehicles.isFetching}
        >
          <RefreshCw size={15} /> Refresh
        </Button>
      </div>
      <form
        className="flex flex-col gap-2 rounded-xl border border-[#dce5df] bg-white p-2.5 sm:flex-row sm:items-center"
        onSubmit={(event) => {
          event.preventDefault()
          if (lookupType === 'published') {
            setScope({ type: 'published', id: '' })
            return
          }
          if (!z.string().uuid().safeParse(lookup.trim()).success) {
            toast.error('Enter a valid UUID.')
            return
          }
          setScope({ type: lookupType, id: lookup.trim() })
        }}
      >
        <select
          aria-label="Vehicle search scope"
          value={lookupType}
          onChange={(event) => setLookupType(event.target.value as typeof lookupType)}
          className="h-9 rounded-lg border border-[#d6e0d9] bg-white px-3 text-sm font-semibold text-[#263733]"
        >
          <option value="published">Published fleet</option>
          <option value="vehicle">Vehicle ID</option>
          <option value="owner">Owner ID</option>
        </select>
        {lookupType !== 'published' && (
          <input
            aria-label="Lookup ID"
            value={lookup}
            onChange={(event) => setLookup(event.target.value)}
            placeholder={`Paste ${lookupType} UUID`}
            className="h-9 min-w-0 flex-1 rounded-lg border border-[#d6e0d9] px-3 font-mono text-sm font-medium outline-none focus:border-[#7ca58a]"
            required
          />
        )}
        <Button type="submit" variant="outline" className="h-9 px-3 text-xs font-bold">
          <Search size={14} />
          Find
        </Button>
        {scope.type !== 'published' && (
          <Button
            type="button"
            variant="ghost"
            className="h-9 px-3 text-xs font-semibold"
            onClick={() => {
              setScope({ type: 'published', id: '' })
              setLookupType('published')
              setLookup('')
            }}
          >
            Clear lookup
          </Button>
        )}
      </form>
      <section className="overflow-hidden rounded-2xl border border-[#e3e9e9] bg-white shadow-[0_3px_14px_#18312b08]">
        <div className="flex flex-col gap-3 border-b border-[#e8eeea] px-4 py-2.5 sm:flex-row sm:items-center sm:justify-between sm:px-5">
          <div>
            <h2 className="m-0 text-base font-bold tracking-[-.015em] text-[#20312c]">
              Fleet listings{' '}
              <span className="ml-1 rounded bg-[#edf2ee] px-1.5 py-0.5 align-middle font-mono text-xs font-bold tabular-nums text-[#52635a]">
                {vehicles.data?.length ?? '—'}
              </span>
            </h2>
            <p className="mb-0 mt-0.5 text-xs font-medium text-[#6f7d74]">
              Data from the fleet service
            </p>
          </div>
          <label className="relative m-0 flex items-center gap-2 sm:w-72">
            <Search size={15} className="absolute left-3 text-[#829087]" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search vehicle or status"
              className="h-9 w-full rounded-lg border border-[#dce5df] pl-9 pr-3 text-sm font-medium text-[#263733] outline-none placeholder:font-normal focus:border-[#7ca58a]"
            />
          </label>
        </div>
        {vehicles.isPending ? (
          <div className="space-y-2 p-4">
            {[0, 1, 2, 3].map((item) => (
              <div key={item} className="h-12 animate-pulse rounded bg-[#f3f6f3]" />
            ))}
          </div>
        ) : rows.length === 0 ? (
          <div className="px-5 py-12 text-center text-sm font-medium text-[#6f7d74]">
            No vehicles found in this scope. Pending listings require a vehicle or owner ID lookup.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[780px] text-left text-sm">
              <thead className="bg-[#f7f9f7] text-xs font-bold uppercase tracking-[.08em] text-[#58675e]">
                <tr>
                  <th className="px-5 py-2.5">Vehicle</th>
                  <th className="px-4 py-2.5">Plate</th>
                  <th className="px-4 py-2.5">Owner ID</th>
                  <th className="px-4 py-2.5">Status</th>
                  <th className="px-4 py-2.5">Seats</th>
                  <th className="px-5 py-2.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((vehicle) => (
                  <tr
                    key={vehicle.vehicleId}
                    className="border-t border-[#e9eeea] text-[#263630] hover:bg-[#f8faf8]"
                  >
                    <td className="px-5 py-3">
                      <strong className="block text-[14px] font-bold tracking-[-.015em] text-[#1d2c27]">
                        {vehicle.brandName} {vehicle.modelName}
                      </strong>
                      <span className="mt-0.5 block text-[11px] font-medium text-[#68776d]">
                        {vehicle.transmission} · {vehicle.address}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-mono text-[13px] font-semibold tabular-nums text-[#43534a]">
                      {vehicle.plateNumber}
                    </td>
                    <td
                      className="max-w-36 truncate px-4 py-3 font-mono text-[11px] font-semibold text-[#5c6b62]"
                      title={vehicle.ownerId}
                    >
                      {vehicle.ownerId}
                    </td>
                    <td className="px-4 py-3">
                      <Badge
                        variant={statusVariant(vehicle.status)}
                        dot
                        className="px-2 py-0.5 text-[11px] font-bold"
                      >
                        {vehicle.status}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-[13px] font-bold tabular-nums text-[#263630]">
                      {vehicle.seats}
                    </td>
                    <td className="px-5 py-3">
                      <div className="flex justify-end gap-1">
                        {vehicle.status === 'PendingApproval' && (
                          <>
                            <button
                              title="Approve listing"
                              onClick={() => mutation.mutate({ vehicle, action: 'approve' })}
                              disabled={mutation.isPending}
                              className="grid size-8 place-items-center rounded-md text-[#38734e] hover:bg-[#edf5ef] disabled:opacity-50"
                            >
                              <Check size={16} />
                            </button>
                            <button
                              title="Reject listing"
                              onClick={() => {
                                setSelected(vehicle)
                                setReason('')
                              }}
                              className="grid size-8 place-items-center rounded-md text-[#a44d43] hover:bg-[#fff0ee]"
                            >
                              <X size={16} />
                            </button>
                          </>
                        )}
                        {vehicle.status === 'Published' && (
                          <button
                            title="Unpublish listing"
                            onClick={() => {
                              setSelected(vehicle)
                              setReason('')
                            }}
                            className="grid size-8 place-items-center rounded-md text-[#a44d43] hover:bg-[#fff0ee]"
                          >
                            <EyeOff size={16} />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      {selected && (
        <div
          className="fixed inset-0 z-50 grid place-items-center bg-[#14251c80] p-4"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) setSelected(null)
          }}
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="vehicle-action-title"
            className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl"
          >
            <p className="mb-2 text-[10px] font-bold tracking-[.14em] text-[#849087]">
              LISTING REVIEW
            </p>
            <h2 id="vehicle-action-title" className="m-0 text-xl font-semibold">
              {selected.status === 'Published' ? 'Unpublish this listing?' : 'Reject this listing?'}
            </h2>
            <p className="mt-2 text-sm text-[#7d8980]">
              {selected.brandName} {selected.modelName} · {selected.plateNumber}
            </p>
            <label className="mt-5 block text-xs font-semibold">
              Reason
              <textarea
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                rows={3}
                maxLength={1000}
                className="mt-2 block w-full rounded-lg border border-[#dfe6e0] p-3 text-sm outline-none focus:border-[#7ca58a]"
                placeholder="Explain why this listing is being changed"
              />
            </label>
            {mutation.isError && (
              <p role="alert" className="mt-3 text-xs text-red-700">
                The API could not complete this action. Please retry.
              </p>
            )}
            <div className="mt-5 flex justify-end gap-2">
              <Button variant="outline" onClick={() => setSelected(null)}>
                Keep listing
              </Button>
              <Button
                variant="destructive"
                disabled={!reason.trim() || mutation.isPending}
                loading={mutation.isPending}
                onClick={() =>
                  mutation.mutate({
                    vehicle: selected,
                    action: selected.status === 'Published' ? 'unpublish' : 'reject',
                    note: reason.trim(),
                  })
                }
              >
                Confirm
              </Button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}

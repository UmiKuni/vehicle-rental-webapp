import { useQuery } from '@tanstack/react-query'
import { ArrowUpRight, CarFront, Check, Layers3, RefreshCw, Users } from 'lucide-react'
import { Link } from 'react-router-dom'
import apiClient from '@/shared/lib/axios'
import { ROUTES } from '@/shared/constants'
import { ErrorState } from '@/shared/components/common/StateViews'

interface AdminVehicle {
  vehicleId: string
  brandName: string
  modelName: string
  plateNumber: string
  status: string
}
interface CatalogEntry {
  brandId?: string
  modelId?: string
  name: string
}

function Metric({
  label,
  value,
  hint,
  icon: Icon,
}: {
  label: string
  value: number
  hint: string
  icon: typeof CarFront
}) {
  return (
    <article className="flex items-start justify-between gap-4 px-5 py-5 sm:px-6 sm:py-6">
      <div>
        <p className="m-0 text-[13px] font-bold text-[#536461]">{label}</p>
        <p className="mb-0 mt-3 font-mono text-[34px] font-bold leading-none tracking-[-.05em] text-[#172725]">
          {value.toLocaleString()}
        </p>
        <p className="mb-0 mt-3 text-xs font-medium text-[#74827f]">{hint}</p>
      </div>
      <Icon size={19} strokeWidth={1.8} className="mt-0.5 shrink-0 text-[#287f7e]" />
    </article>
  )
}

export default function DashboardPage() {
  const vehicles = useQuery({
    queryKey: ['admin', 'fleet'],
    queryFn: async () => (await apiClient.get<AdminVehicle[]>('/fleet/vehicles')).data,
  })
  const brands = useQuery({
    queryKey: ['admin', 'brands'],
    queryFn: async () => (await apiClient.get<CatalogEntry[]>('/fleet/brands')).data,
  })
  const models = useQuery({
    queryKey: ['admin', 'models'],
    queryFn: async () => (await apiClient.get<CatalogEntry[]>('/fleet/models')).data,
  })

  if (vehicles.isError)
    return (
      <ErrorState
        message="Could not load fleet data from the API."
        onRetry={() => void vehicles.refetch()}
      />
    )
  const rows = vehicles.data ?? []
  const published = rows.filter((vehicle) => vehicle.status === 'Published')
  const recent = [...rows].slice(-5).reverse()

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 border-b border-[#dfe7e6] pb-5 sm:flex-row sm:items-end">
        <div>
          <p className="mb-1.5 text-[10px] font-extrabold tracking-[.14em] text-[#287f7e]">
            PLATFORM OPERATIONS
          </p>
          <h1 className="m-0 text-[27px] font-extrabold leading-tight tracking-[-.04em] text-[#172725] sm:text-[30px]">
            Operations overview
          </h1>
          <p className="mb-0 mt-1.5 text-sm font-semibold text-[#687773]">
            Current fleet and catalog data from the API.
          </p>
        </div>
        <button
          onClick={() => {
            void vehicles.refetch()
            void brands.refetch()
            void models.refetch()
          }}
          className="inline-flex h-11 items-center justify-center gap-2 self-start rounded-lg border border-[#d6e2e0] bg-white px-4 text-[13px] font-bold text-[#3d5552] shadow-sm transition hover:border-[#9dc9c8] hover:bg-[#f4fafa] active:scale-[.98] sm:self-auto"
        >
          <RefreshCw size={16} className={vehicles.isFetching ? 'animate-spin' : ''} />
          Refresh data
        </button>
      </div>

      <div className="grid overflow-hidden rounded-xl border border-[#dfe7e6] bg-white divide-y sm:grid-cols-2 sm:divide-x sm:divide-y-0 xl:grid-cols-3">
        <Metric
          label="Published vehicles"
          value={published.length}
          hint="Visible in customer search"
          icon={CarFront}
        />
        <Metric
          label="Vehicle brands"
          value={brands.data?.length ?? 0}
          hint="Available for fleet listings"
          icon={Layers3}
        />
        <Metric
          label="Vehicle models"
          value={models.data?.length ?? 0}
          hint="Listed in the catalog"
          icon={Check}
        />
      </div>

      {vehicles.isPending && (
        <div className="h-1 overflow-hidden rounded bg-[#e7ece8]">
          <div className="h-full w-1/3 animate-pulse bg-[#4b8360]" />
        </div>
      )}

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1.65fr)_minmax(280px,.85fr)]">
        <section className="overflow-hidden rounded-xl border border-[#dfe7e6] bg-white">
          <div className="flex items-center justify-between border-b border-[#eaf0ef] px-6 py-5">
            <div>
              <h2 className="m-0 text-[17px] font-extrabold tracking-[-.025em] text-[#1b2c29]">
                Fleet snapshot
              </h2>
              <p className="mb-0 mt-1.5 text-xs font-medium text-[#82908e]">
                Published listings returned by the fleet API
              </p>
            </div>
            <Link
              to={ROUTES.ADMIN_VEHICLES}
              className="inline-flex items-center gap-1 text-[13px] font-bold text-[#168487] transition hover:text-[#0e6669]"
            >
              View fleet <ArrowUpRight size={15} />
            </Link>
          </div>
          {vehicles.isPending ? (
            <div className="space-y-2 p-4">
              {[0, 1, 2].map((i) => (
                <div key={i} className="h-12 animate-pulse rounded bg-[#f3f6f3]" />
              ))}
            </div>
          ) : recent.length ? (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[520px] text-left text-sm">
                <thead className="bg-[#f7f9f7] text-xs font-bold uppercase tracking-[.08em] text-[#58675e]">
                  <tr>
                    <th className="px-5 py-2.5">Vehicle</th>
                    <th className="px-4 py-2.5">Plate</th>
                    <th className="px-4 py-2.5">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {recent.map((vehicle) => (
                    <tr
                      key={vehicle.vehicleId}
                      className="border-t border-[#e9eeea] text-[#263630]"
                    >
                      <td className="px-5 py-2.5 text-sm font-bold text-[#1d2c27]">
                        {vehicle.brandName} {vehicle.modelName}
                      </td>
                      <td className="px-4 py-2.5 font-mono text-[13px] font-semibold tabular-nums text-[#43534a]">
                        {vehicle.plateNumber}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="rounded-md bg-[#edf2ee] px-2 py-0.5 text-[11px] font-bold text-[#52635a]">
                          {vehicle.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="p-8 text-center text-sm font-medium text-[#68776d]">
              No fleet records were returned.
            </div>
          )}
        </section>
        <section className="rounded-xl border border-[#dfe7e6] bg-white p-5 sm:p-6">
          <p className="mb-1.5 text-[10px] font-semibold tracking-[.12em] text-[#287f7e]">
            SHORTCUTS
          </p>
          <h2 className="mb-4 mt-0 text-[17px] font-semibold tracking-[-.02em] text-[#1b2c29]">
            Manage platform
          </h2>
          <div className="grid">
            <Link
              to={ROUTES.ADMIN_VEHICLES}
              className="group flex items-center gap-3 border-b border-[#edf1f0] py-3 transition hover:text-[#176f70]"
            >
              <CarFront size={18} className="shrink-0 text-[#52716e]" />
              <span className="min-w-0 flex-1">
                <strong className="block text-[13px] font-semibold">Fleet vehicles</strong>
                <small className="mt-1 block text-[11px] text-[#85928f]">
                  Review listing status
                </small>
              </span>
              <ArrowUpRight
                size={15}
                className="text-[#9aa8a6] transition group-hover:-translate-y-0.5 group-hover:translate-x-0.5"
              />
            </Link>
            <Link
              to={ROUTES.ADMIN_CATALOG}
              className="group flex items-center gap-3 border-b border-[#edf1f0] py-3 transition hover:text-[#176f70]"
            >
              <Layers3 size={18} className="shrink-0 text-[#52716e]" />
              <span className="min-w-0 flex-1">
                <strong className="block text-[13px] font-semibold">Vehicle catalog</strong>
                <small className="mt-1 block text-[11px] text-[#85928f]">
                  Maintain brands and models
                </small>
              </span>
              <ArrowUpRight
                size={15}
                className="text-[#9aa8a6] transition group-hover:-translate-y-0.5 group-hover:translate-x-0.5"
              />
            </Link>
            <Link
              to={ROUTES.USERS}
              className="group flex items-center gap-3 py-3 transition hover:text-[#176f70]"
            >
              <Users size={18} className="shrink-0 text-[#52716e]" />
              <span className="min-w-0 flex-1">
                <strong className="block text-[13px] font-semibold">User access</strong>
                <small className="mt-1 block text-[11px] text-[#85928f]">
                  Suspend or restore access
                </small>
              </span>
              <ArrowUpRight
                size={15}
                className="text-[#9aa8a6] transition group-hover:-translate-y-0.5 group-hover:translate-x-0.5"
              />
            </Link>
          </div>
          <p className="mb-0 mt-4 rounded-lg bg-[#f5f7f5] px-3.5 py-3 text-[11px] leading-relaxed text-[#7b877e]">
            The BE fleet API does not yet provide a pending-review queue. Use vehicle or owner ID
            lookup to review a specific listing.
          </p>
        </section>
      </div>
    </div>
  )
}

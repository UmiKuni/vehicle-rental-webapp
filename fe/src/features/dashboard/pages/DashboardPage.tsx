import { useQuery } from '@tanstack/react-query'
import { DollarSign, Car, CalendarCheck, AlertTriangle, Plus, Download } from 'lucide-react'
import { FleetMetricCard } from '../components/FleetMetricCard'
import { RevenueTrendChart } from '../components/RevenueTrendChart'
import { FleetStatusBreakdown } from '../components/FleetStatusBreakdown'
import { RecentRentalsTable } from '../components/RecentRentalsTable'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ErrorState } from '@/shared/components/common/StateViews'
import { Button } from '@/shared/components/ui/Button'
import { QUERY_KEYS } from '@/shared/constants'

async function fetchDashboardStats() {
  await new Promise((r) => setTimeout(r, 300))
  return {
    totalRevenue: 158_400_000,
    revenueGrowth: 24.1,
    utilizationRate: 84.5,
    activeVehicles: 54,
    totalVehicles: 64,
    activeRentals: 18,
    rentalGrowth: 12.5,
    maintenanceAlerts: 3,
  }
}

export default function DashboardPage() {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: [QUERY_KEYS.DASHBOARD_STATS],
    queryFn: fetchDashboardStats,
    staleTime: 5 * 60 * 1000,
  })

  if (isError) return <ErrorState onRetry={refetch} />

  return (
    <div className="space-y-6">
      {/* Operations Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between border-b border-border/60 pb-5">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold tracking-tight text-foreground sm:text-2xl">
              Tổng quan vận hành đội xe
            </h1>
            <span className="rounded-md border border-emerald-500/30 bg-emerald-500/10 px-2 py-0.5 text-[11px] font-semibold text-emerald-600 dark:text-emerald-400">
              Live Telemetry
            </span>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            Báo cáo tức thời hiệu suất 64 phương tiện, tỷ lệ khai thác và doanh thu MTD
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" className="h-9 gap-1.5 text-xs font-medium">
            <Download className="size-3.5" />
            Xuất báo cáo
          </Button>
          <Button size="sm" className="h-9 gap-1.5 text-xs font-semibold shadow-xs">
            <Plus className="size-3.5" />
            Tạo đơn đặt xe
          </Button>
        </div>
      </div>

      {/* KPI Grid (Level 6 density) */}
      <div className="grid gap-3.5 sm:grid-cols-2 xl:grid-cols-4">
        {isLoading ? (
          Array.from({ length: 4 }).map((_, i) => <CardSkeleton key={i} />)
        ) : (
          <>
            <FleetMetricCard
              label="Doanh thu lũy kế (MTD)"
              value={new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', notation: 'compact' }).format(data!.totalRevenue)}
              trend={data!.revenueGrowth}
              icon={DollarSign}
              statusColor="blue"
            />
            <FleetMetricCard
              label="Tỷ lệ khai thác xe"
              value={`${data!.utilizationRate}%`}
              subValue={`${data!.activeVehicles}/${data!.totalVehicles} xe`}
              trend={4.2}
              icon={Car}
              statusColor="emerald"
            />
            <FleetMetricCard
              label="Đơn thuê đang chạy"
              value={`${data!.activeRentals} đơn`}
              trend={data!.rentalGrowth}
              icon={CalendarCheck}
              statusColor="blue"
            />
            <FleetMetricCard
              label="Cảnh báo kiểm định / bảo dưỡng"
              value={`${data!.maintenanceAlerts} xe`}
              subValue="2 quá hạn"
              trend={-1}
              trendLabel="so với tuần trước"
              icon={AlertTriangle}
              statusColor="amber"
            />
          </>
        )}
      </div>

      {/* Analytics & Distribution Grid */}
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <RevenueTrendChart />
        </div>
        <div>
          <FleetStatusBreakdown />
        </div>
      </div>

      {/* Operations Live Stream */}
      <div>
        <RecentRentalsTable />
      </div>
    </div>
  )
}

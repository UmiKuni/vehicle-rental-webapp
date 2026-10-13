import { ArrowUpRight } from 'lucide-react'

interface RentalItem {
  id: string
  customer: string
  phone: string
  vehicle: string
  plate: string
  hub: string
  duration: string
  amount: number
  status: 'ACTIVE' | 'PENDING' | 'COMPLETED'
}

const RECENT_RENTALS: RentalItem[] = [
  {
    id: 'RNT-2026-089',
    customer: 'Nguyễn Tiến Dũng',
    phone: '0982 112 334',
    vehicle: 'VinFast VF9 Plus',
    plate: '30E-891.22',
    hub: 'Hub Cầu Giấy',
    duration: '12/10 - 15/10',
    amount: 5_400_000,
    status: 'ACTIVE',
  },
  {
    id: 'RNT-2026-088',
    customer: 'Trần Minh Hoàng',
    phone: '0912 445 667',
    vehicle: 'Mercedes C300 AMG',
    plate: '29A-456.78',
    hub: 'Hub Hoàn Kiếm',
    duration: '13/10 - 14/10',
    amount: 3_200_000,
    status: 'ACTIVE',
  },
  {
    id: 'RNT-2026-087',
    customer: 'Lê Thùy Trang',
    phone: '0903 778 899',
    vehicle: 'Hyundai Santa Fe',
    plate: '30G-771.02',
    hub: 'Hub Tân Bình',
    duration: '14/10 - 18/10',
    amount: 6_800_000,
    status: 'PENDING',
  },
  {
    id: 'RNT-2026-086',
    customer: 'Phạm Đức Anh',
    phone: '0974 556 123',
    vehicle: 'Toyota Camry 2.5Q',
    plate: '51H-332.19',
    hub: 'Hub Quận 1',
    duration: '10/10 - 12/10',
    amount: 4_100_000,
    status: 'COMPLETED',
  },
]

export function RecentRentalsTable() {
  return (
    <div className="rounded-xl border border-border bg-card shadow-2xs overflow-hidden">
      <div className="flex items-center justify-between border-b border-border/60 p-4 sm:px-6">
        <div>
          <h3 className="text-sm font-semibold text-foreground">Đơn đặt thuê & bàn giao xe mới nhất</h3>
          <p className="mt-0.5 text-xs text-muted-foreground">Theo dõi hành trình nhận xe và thanh toán tức thì</p>
        </div>
        <button className="inline-flex items-center gap-1 text-xs font-semibold text-primary hover:underline">
          Xem tất cả <ArrowUpRight className="size-3.5" />
        </button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-border/60 bg-muted/30 text-muted-foreground">
              <th className="px-4 py-3 text-left font-semibold">Mã đơn</th>
              <th className="px-4 py-3 text-left font-semibold">Khách hàng</th>
              <th className="px-4 py-3 text-left font-semibold">Phương tiện & Biển số</th>
              <th className="px-4 py-3 text-left font-semibold">Trạm xe</th>
              <th className="px-4 py-3 text-left font-semibold">Thời hạn</th>
              <th className="px-4 py-3 text-right font-semibold">Tổng tiền</th>
              <th className="px-4 py-3 text-center font-semibold">Trạng thái</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/40">
            {RECENT_RENTALS.map((item) => (
              <tr key={item.id} className="hover:bg-muted/20 transition-colors">
                <td className="px-4 py-3 font-mono font-medium text-foreground">{item.id}</td>
                <td className="px-4 py-3">
                  <p className="font-semibold text-foreground">{item.customer}</p>
                  <p className="font-mono text-[10px] text-muted-foreground">{item.phone}</p>
                </td>
                <td className="px-4 py-3">
                  <p className="font-medium text-foreground">{item.vehicle}</p>
                  <span className="inline-block rounded border border-border bg-muted/60 px-1.5 py-0.2 font-mono text-[10px] text-foreground font-semibold">
                    {item.plate}
                  </span>
                </td>
                <td className="px-4 py-3 text-muted-foreground">{item.hub}</td>
                <td className="px-4 py-3 font-mono text-muted-foreground">{item.duration}</td>
                <td className="px-4 py-3 text-right font-mono font-semibold text-foreground">
                  {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(item.amount)}
                </td>
                <td className="px-4 py-3 text-center">
                  {item.status === 'ACTIVE' && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-emerald-500/10 px-2 py-0.5 text-[10px] font-semibold text-emerald-600 dark:text-emerald-400 border border-emerald-500/20">
                      <span className="size-1.5 rounded-full bg-emerald-500 animate-pulse" />
                      Đang chạy
                    </span>
                  )}
                  {item.status === 'PENDING' && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-blue-500/10 px-2 py-0.5 text-[10px] font-semibold text-blue-600 dark:text-blue-400 border border-blue-500/20">
                      <span className="size-1.5 rounded-full bg-blue-500" />
                      Chờ bàn giao
                    </span>
                  )}
                  {item.status === 'COMPLETED' && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-muted px-2 py-0.5 text-[10px] font-semibold text-muted-foreground border border-border">
                      Hoàn tất
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

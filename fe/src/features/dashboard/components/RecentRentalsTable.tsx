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
    customer: 'Daniel Craig',
    phone: '0982 112 334',
    vehicle: 'VinFast VF9 Plus',
    plate: '30E-891.22',
    hub: 'Cau Giay Hub',
    duration: '10/12 - 10/15',
    amount: 5_400_000,
    status: 'ACTIVE',
  },
  {
    id: 'RNT-2026-088',
    customer: 'Thomas Miller',
    phone: '0912 445 667',
    vehicle: 'Mercedes C300 AMG',
    plate: '29A-456.78',
    hub: 'Hoan Kiem Hub',
    duration: '10/13 - 10/14',
    amount: 3_200_000,
    status: 'ACTIVE',
  },
  {
    id: 'RNT-2026-087',
    customer: 'Jessica Taylor',
    phone: '0903 778 899',
    vehicle: 'Hyundai Santa Fe',
    plate: '30G-771.02',
    hub: 'Tan Binh Hub',
    duration: '10/14 - 10/18',
    amount: 6_800_000,
    status: 'PENDING',
  },
  {
    id: 'RNT-2026-086',
    customer: 'Alex Anderson',
    phone: '0974 556 123',
    vehicle: 'Toyota Camry 2.5Q',
    plate: '51H-332.19',
    hub: 'District 1 Hub',
    duration: '10/10 - 10/12',
    amount: 4_100_000,
    status: 'COMPLETED',
  },
]

export function RecentRentalsTable() {
  return (
    <div className="rounded-xl border border-border bg-card shadow-2xs overflow-hidden">
      <div className="flex items-center justify-between border-b border-border/60 p-4 sm:px-6">
        <div>
          <h3 className="text-sm font-semibold text-foreground">Recent Rental & Delivery Orders</h3>
          <p className="mt-0.5 text-xs text-muted-foreground">Track real-time vehicle handovers and instant payments</p>
        </div>
        <button className="inline-flex items-center gap-1 text-xs font-semibold text-primary hover:underline">
          View all <ArrowUpRight className="size-3.5" />
        </button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-border/60 bg-muted/30 text-muted-foreground">
              <th className="px-4 py-3 text-left font-semibold">Order ID</th>
              <th className="px-4 py-3 text-left font-semibold">Customer</th>
              <th className="px-4 py-3 text-left font-semibold">Vehicle & Plate</th>
              <th className="px-4 py-3 text-left font-semibold">Station Hub</th>
              <th className="px-4 py-3 text-left font-semibold">Duration</th>
              <th className="px-4 py-3 text-right font-semibold">Total Amount</th>
              <th className="px-4 py-3 text-center font-semibold">Status</th>
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
                  {new Intl.NumberFormat('en-US', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(item.amount)}
                </td>
                <td className="px-4 py-3 text-center">
                  {item.status === 'ACTIVE' && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-emerald-500/10 px-2 py-0.5 text-[10px] font-semibold text-emerald-600 dark:text-emerald-400 border border-emerald-500/20">
                      <span className="size-1.5 rounded-full bg-emerald-500 animate-pulse" />
                      Active
                    </span>
                  )}
                  {item.status === 'PENDING' && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-blue-500/10 px-2 py-0.5 text-[10px] font-semibold text-blue-600 dark:text-blue-400 border border-blue-500/20">
                      <span className="size-1.5 rounded-full bg-blue-500" />
                      Pending Handover
                    </span>
                  )}
                  {item.status === 'COMPLETED' && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-muted px-2 py-0.5 text-[10px] font-semibold text-muted-foreground border border-border">
                      Completed
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

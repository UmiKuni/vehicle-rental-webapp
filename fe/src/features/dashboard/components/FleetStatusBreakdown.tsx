import { Car, Wrench, CheckCircle2, Clock } from 'lucide-react'

const FLEET_SEGMENTS = [
  { label: 'Rented Out', count: 42, percent: '65.6%', color: 'bg-emerald-500' },
  { label: 'Ready at Hub', count: 16, percent: '25.0%', color: 'bg-blue-500' },
  { label: 'Maintenance', count: 4, percent: '6.3%', color: 'bg-amber-500' },
  { label: 'Inspection', count: 2, percent: '3.1%', color: 'bg-zinc-500' },
]

const KEY_VEHICLES = [
  { model: 'VinFast VF9 Plus', plate: '30E-891.22', type: 'EV Suv', status: 'Rented', icon: Clock, color: 'text-emerald-500 bg-emerald-500/10' },
  { model: 'Mercedes C300 AMG', plate: '29A-456.78', type: 'Sedan', status: 'Ready', icon: CheckCircle2, color: 'text-blue-500 bg-blue-500/10' },
  { model: 'Ford Everest', plate: '51F-123.45', type: 'SUV 7C', status: 'Rented', icon: Clock, color: 'text-emerald-500 bg-emerald-500/10' },
  { model: 'Porsche Macan', plate: '30H-999.88', type: 'Luxury', status: 'Maintenance', icon: Wrench, color: 'text-amber-500 bg-amber-500/10' },
]

export function FleetStatusBreakdown() {
  return (
    <div className="rounded-xl border border-border bg-card p-5 shadow-2xs">
      <div className="flex items-center justify-between border-b border-border/60 pb-3">
        <div>
          <h3 className="text-sm font-semibold text-foreground">Fleet Status Breakdown</h3>
          <p className="mt-0.5 text-xs text-muted-foreground">Status of 64 vehicles across handover stations</p>
        </div>
        <div className="flex items-center gap-1.5 rounded-md border border-border bg-muted/40 px-2 py-1 text-xs font-mono font-medium text-foreground">
          <Car className="size-3.5 text-primary" />
          <span>64 Vehicles</span>
        </div>
      </div>

      {/* Segmented Progress Bar */}
      <div className="mt-4 space-y-2">
        <div className="h-2 w-full rounded-full bg-border flex overflow-hidden">
          <div style={{ width: '65.6%' }} className="bg-emerald-500" title="Rented: 65.6%" />
          <div style={{ width: '25.0%' }} className="bg-blue-500" title="Ready: 25.0%" />
          <div style={{ width: '6.3%' }} className="bg-amber-500" title="Maintenance: 6.3%" />
          <div style={{ width: '3.1%' }} className="bg-zinc-500" title="Inspection: 3.1%" />
        </div>

        {/* Legend */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-1 text-[11px]">
          {FLEET_SEGMENTS.map((seg) => (
            <div key={seg.label} className="flex items-center gap-1.5">
              <span className={`size-2 rounded-full ${seg.color}`} />
              <span className="text-muted-foreground">{seg.label}:</span>
              <strong className="font-mono text-foreground">{seg.count}</strong>
            </div>
          ))}
        </div>
      </div>

      {/* Key Vehicles Stream */}
      <div className="mt-5 space-y-2 border-t border-border/60 pt-4">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">Highlighted Fleet Monitoring</p>
        {KEY_VEHICLES.map((v) => {
          const Icon = v.icon
          return (
            <div key={v.plate} className="flex items-center justify-between rounded-lg border border-border/40 bg-muted/20 p-2.5 hover:bg-muted/40 transition-colors">
              <div className="flex items-center gap-2.5">
                <div className={`p-1.5 rounded-md ${v.color}`}>
                  <Icon className="size-3.5" />
                </div>
                <div>
                  <p className="text-xs font-semibold text-foreground">{v.model}</p>
                  <p className="text-[10px] font-mono text-muted-foreground">{v.plate} • {v.type}</p>
                </div>
              </div>
              <span className="text-[11px] font-medium text-foreground px-2 py-0.5 rounded border border-border bg-card">
                {v.status}
              </span>
            </div>
          )
        })}
      </div>
    </div>
  )
}

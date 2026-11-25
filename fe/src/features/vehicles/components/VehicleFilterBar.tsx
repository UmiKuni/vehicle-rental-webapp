import { useState } from 'react'
import { CalendarDays, Filter, MapPin, RotateCcw, Search, SlidersHorizontal } from 'lucide-react'
import type { VehicleFilterParams } from '@/types/vehicle.types'
import { Dialog, DialogContent, DialogHeader } from '@/shared/components/ui/Dialog'

interface VehicleFilterBarProps {
  location: string
  locationFromGoogle: boolean
  onLocate: () => void
  locating: boolean
  filters: VehicleFilterParams
  onApply: (filters: VehicleFilterParams) => void
}

const fieldClass = 'h-11 w-full rounded-xl border border-border bg-background px-3 text-sm text-foreground outline-none focus:border-teal-600 focus:ring-2 focus:ring-teal-600/15'
const fieldLabel = 'mb-1.5 block text-xs font-semibold text-foreground'

function addDays(value: string, days: number) {
  const date = new Date(`${value}T12:00:00`)
  date.setDate(date.getDate() + days)
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

export function VehicleFilterBar({ location, locationFromGoogle, onLocate, locating, filters, onApply }: VehicleFilterBarProps) {
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<VehicleFilterParams>(filters)
  const [query, setQuery] = useState(filters.search ?? '')
  const invalidPriceRange = draft.minPrice != null && draft.maxPrice != null && draft.minPrice > draft.maxPrice
  const update = <K extends keyof VehicleFilterParams>(key: K, value: VehicleFilterParams[K]) => setDraft((prev) => ({ ...prev, [key]: value }))

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    if (invalidPriceRange) return
    onApply({ ...draft, search: query.trim() || undefined })
    setOpen(false)
  }
  const tomorrow = new Date()
  tomorrow.setDate(tomorrow.getDate() + 1)
  const minimumDate = `${tomorrow.getFullYear()}-${String(tomorrow.getMonth() + 1).padStart(2, '0')}-${String(tomorrow.getDate()).padStart(2, '0')}`
  const clearFilters = () => {
    setQuery('')
    setDraft({ latitude: filters.latitude, longitude: filters.longitude, transmission: 'ALL' })
  }
  const changePickupDate = (value: string) => {
    update('pickupAt', value || undefined)
    if (value && draft.returnAt && draft.returnAt <= value) update('returnAt', addDays(value, 1))
  }

  return (
    <>
      <div className="search-toolbar">
        <label className="search-location">
          <MapPin size={18} className="search-location-icon" />
          <span className="search-location-copy"><small>Pickup area{locationFromGoogle && <span className="location-attribution"> · Map lookup</span>}</small><input value={location} readOnly aria-label="Search area" placeholder="Finding your location..." /></span>
        </label>
        <button type="button" onClick={onLocate} disabled={locating} className="locate-button" aria-label="Use my current location"><MapPin size={17} /></button>
        <button type="button" className="filter-open-button" onClick={() => { setDraft(filters); setOpen(true) }}><SlidersHorizontal size={17} /><span>Filters</span></button>
      </div>
      <div className="search-query-row"><Search size={16} /><input value={query} onChange={(event) => { const value = event.target.value; setQuery(value); onApply({ ...filters, search: value.trim() || undefined }) }} placeholder="Search by car or brand" aria-label="Search by car or brand" />{query && <button type="button" onClick={() => { setQuery(''); onApply({ ...filters, search: undefined }) }}>Clear</button>}</div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="vehicle-filter-dialog">
            <DialogHeader><span className="filter-dialog-kicker"><Filter size={14} /> SEARCH PREFERENCES</span></DialogHeader>
          <form onSubmit={submit} className="filter-form">
            <div className="filter-date-grid">
              <label><span className={fieldLabel}>Pickup</span><span className="filter-input-icon"><CalendarDays size={15} /><input className={fieldClass} type="date" min={minimumDate} value={draft.pickupAt?.slice(0, 10) ?? ''} onChange={(e) => changePickupDate(e.target.value)} /></span></label>
              <label><span className={fieldLabel}>Return</span><span className="filter-input-icon"><CalendarDays size={15} /><input className={fieldClass} type="date" min={draft.pickupAt ? addDays(draft.pickupAt.slice(0, 10), 1) : minimumDate} value={draft.returnAt?.slice(0, 10) ?? ''} onChange={(e) => update('returnAt', e.target.value || undefined)} /></span></label>
            </div>
            <div className="filter-date-grid"><label><span className={fieldLabel}>Min price (VND / day)</span><input className={fieldClass} type="number" min="0" step="50000" value={draft.minPrice ?? ''} onChange={(e) => update('minPrice', e.target.value ? Number(e.target.value) : undefined)} placeholder="No minimum" /></label><label><span className={fieldLabel}>Max price (VND / day)</span><input className={fieldClass} type="number" min="0" step="50000" value={draft.maxPrice ?? ''} onChange={(e) => update('maxPrice', e.target.value ? Number(e.target.value) : undefined)} placeholder="No maximum" /></label></div>
            <label><span className={fieldLabel}>Make</span><input className={fieldClass} list="vehicle-brands" value={draft.brand ?? ''} onChange={(e) => update('brand', e.target.value || undefined)} placeholder="e.g. Toyota, VinFast" /><datalist id="vehicle-brands"><option value="Toyota" /><option value="VinFast" /><option value="Mazda" /><option value="Hyundai" /><option value="Mitsubishi" /><option value="Ford" /><option value="Honda" /></datalist></label>
            <div className="filter-date-grid"><label><span className={fieldLabel}>Seats</span><select className={fieldClass} value={draft.seats ?? ''} onChange={(e) => update('seats', e.target.value ? Number(e.target.value) : undefined)}><option value="">Any</option>{[1,2,3,4,5,6,7,8,9].map((n) => <option key={n} value={n}>{n} seats</option>)}</select></label><label><span className={fieldLabel}>Transmission</span><select className={fieldClass} value={draft.transmission ?? 'ALL'} onChange={(e) => update('transmission', e.target.value as VehicleFilterParams['transmission'])}><option value="ALL">Any</option><option value="AUTOMATIC">Automatic</option><option value="MANUAL">Manual</option></select></label></div>
            {invalidPriceRange && <p role="alert" className="text-xs text-destructive">Minimum price must not exceed maximum price.</p>}
            <div className="filter-form-footer"><button type="button" className="filter-reset" onClick={clearFilters}><RotateCcw size={15} /> Clear filters</button><button type="submit" className="filter-submit" disabled={invalidPriceRange}><Filter size={15} /> Show results</button></div>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}

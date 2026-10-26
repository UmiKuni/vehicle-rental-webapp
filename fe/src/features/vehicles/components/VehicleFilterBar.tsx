import { useState } from 'react'
import { CalendarDays, CarFront, Filter, MapPin, RotateCcw, Search, SlidersHorizontal } from 'lucide-react'
import type { VehicleFilterParams } from '@/types/vehicle.types'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/shared/components/ui/Dialog'

interface VehicleFilterBarProps {
  location: string
  onLocationChange: (value: string) => void
  onLocate: () => void
  locating: boolean
  filters: VehicleFilterParams
  onApply: (filters: VehicleFilterParams) => void
}

const fieldClass = 'h-11 w-full rounded-xl border border-border bg-background px-3 text-sm text-foreground outline-none focus:border-teal-600 focus:ring-2 focus:ring-teal-600/15'
const fieldLabel = 'mb-1.5 block text-xs font-semibold text-foreground'

export function VehicleFilterBar({ location, onLocationChange, onLocate, locating, filters, onApply }: VehicleFilterBarProps) {
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<VehicleFilterParams>(filters)
  const [query, setQuery] = useState('')
  const update = <K extends keyof VehicleFilterParams>(key: K, value: VehicleFilterParams[K]) => setDraft((prev) => ({ ...prev, [key]: value }))

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    onApply({ ...draft, search: query.trim() || undefined })
    setOpen(false)
  }

  return (
    <>
      <div className="search-toolbar">
        <label className="search-location">
          <MapPin size={18} className="search-location-icon" />
          <span className="search-location-copy"><small>Khu vực nhận xe</small><input value={location} onChange={(event) => onLocationChange(event.target.value)} aria-label="Khu vực tìm xe" placeholder="Đang tìm vị trí..." /></span>
        </label>
        <button type="button" onClick={onLocate} disabled={locating} className="locate-button" aria-label="Lấy vị trí hiện tại"><MapPin size={17} />{locating ? 'Đang định vị' : 'Vị trí của tôi'}</button>
        <button type="button" className="filter-open-button" onClick={() => { setDraft(filters); setOpen(true) }}><SlidersHorizontal size={17} /><span>Lọc</span></button>
      </div>
      <div className="search-query-row"><Search size={16} /><input value={query} onChange={(event) => { const value = event.target.value; setQuery(value); onApply({ ...filters, search: value.trim() || undefined }) }} placeholder="Tìm theo tên xe hoặc hãng xe" aria-label="Tìm theo tên xe hoặc hãng xe" />{query && <button type="button" onClick={() => { setQuery(''); onApply({ ...filters, search: undefined }) }}>Xóa</button>}</div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="vehicle-filter-dialog">
          <DialogHeader><span className="filter-dialog-kicker"><Filter size={14} /> TÙY CHỌN TÌM XE</span><DialogTitle>Bộ lọc chi tiết</DialogTitle><DialogDescription>Chọn tiêu chí để tìm chiếc xe phù hợp với hành trình.</DialogDescription></DialogHeader>
          <form onSubmit={submit} className="filter-form">
            <div className="filter-date-grid">
              <label><span className={fieldLabel}>Nhận xe</span><span className="filter-input-icon"><CalendarDays size={15} /><input className={fieldClass} type="datetime-local" value={draft.pickupAt ?? ''} onChange={(e) => update('pickupAt', e.target.value || undefined)} /></span></label>
              <label><span className={fieldLabel}>Trả xe</span><span className="filter-input-icon"><CalendarDays size={15} /><input className={fieldClass} type="datetime-local" value={draft.returnAt ?? ''} onChange={(e) => update('returnAt', e.target.value || undefined)} /></span></label>
            </div>
            <label><span className={fieldLabel}>Loại xe</span><span className="filter-input-icon"><CarFront size={16} /><input className={fieldClass} list="vehicle-categories" value={draft.category === 'ALL' ? '' : draft.category ?? ''} onChange={(e) => update('category', e.target.value as VehicleFilterParams['category'])} placeholder="Chọn hoặc nhập loại xe" /><datalist id="vehicle-categories"><option value="CAR_4_SEAT">Ô tô 4–5 chỗ</option><option value="CAR_7_SEAT">Ô tô 7 chỗ</option><option value="ELECTRIC">Xe điện</option><option value="TRUCK">Xe bán tải</option><option value="MOTORBIKE">Xe máy</option></datalist></span></label>
            <div className="filter-date-grid"><label><span className={fieldLabel}>Giá thấp nhất (₫ / ngày)</span><input className={fieldClass} type="number" min="0" step="50000" value={draft.minPrice ?? ''} onChange={(e) => update('minPrice', e.target.value ? Number(e.target.value) : undefined)} placeholder="Không giới hạn" /></label><label><span className={fieldLabel}>Giá cao nhất (₫ / ngày)</span><input className={fieldClass} type="number" min="0" step="50000" value={draft.maxPrice ?? ''} onChange={(e) => update('maxPrice', e.target.value ? Number(e.target.value) : undefined)} placeholder="Không giới hạn" /></label></div>
            <label><span className={fieldLabel}>Hãng xe</span><input className={fieldClass} list="vehicle-brands" value={draft.brand ?? ''} onChange={(e) => update('brand', e.target.value || undefined)} placeholder="Ví dụ: Toyota, VinFast" /><datalist id="vehicle-brands"><option value="Toyota" /><option value="VinFast" /><option value="Mazda" /><option value="Hyundai" /><option value="Mitsubishi" /><option value="Ford" /><option value="Honda" /></datalist></label>
            <div className="filter-date-grid"><label><span className={fieldLabel}>Số chỗ</span><select className={fieldClass} value={draft.seats ?? ''} onChange={(e) => update('seats', e.target.value ? Number(e.target.value) : undefined)}><option value="">Bất kỳ</option>{[1,2,3,4,5,6,7,8,9].map((n) => <option key={n} value={n}>{n} chỗ</option>)}</select></label><label><span className={fieldLabel}>Hộp số</span><select className={fieldClass} value={draft.transmission ?? 'ALL'} onChange={(e) => update('transmission', e.target.value as VehicleFilterParams['transmission'])}><option value="ALL">Bất kỳ</option><option value="AUTOMATIC">Tự động</option><option value="MANUAL">Số sàn</option></select></label></div>
            <div className="filter-form-footer"><button type="button" className="filter-reset" onClick={() => setDraft({ transmission: 'ALL', category: 'ALL' })}><RotateCcw size={15} /> Xóa bộ lọc</button><button type="submit" className="filter-submit"><Filter size={15} /> Xem kết quả</button></div>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}

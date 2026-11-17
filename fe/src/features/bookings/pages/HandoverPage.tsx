import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { ArrowLeft, Camera, CheckCircle2, Gauge, Fuel, LoaderCircle, ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'
import apiClient from '@/shared/lib/axios'

type HandoverType = 'Pickup' | 'Return'
interface HandoverSummary { id: string; type: string; status: string }
interface HandoverDetail { id: string; bookingId: string; status: string; condition?: { odo: number; fuelLevel: number; damageNote?: string | null } }

export default function HandoverPage() {
  const { bookingId = '', type = 'pickup' } = useParams()
  const location = useLocation()
  const handoverType: HandoverType = (type || location.pathname.split('/').pop() || 'pickup').toLowerCase() === 'return' ? 'Return' : 'Pickup'
  const title = handoverType === 'Pickup' ? 'Pickup inspection' : 'Return inspection'
  const [handoverId, setHandoverId] = useState('')
  const [status, setStatus] = useState('')
  const [odo, setOdo] = useState('')
  const [fuel, setFuel] = useState('100')
  const [damageNote, setDamageNote] = useState('')
  const [photos, setPhotos] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(true)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    const load = async () => {
      try {
        const { data } = await apiClient.get<HandoverSummary[]>(`/handovers/booking/${bookingId}`)
        const match = data.find((item) => item.type.toLowerCase() === handoverType.toLowerCase())
        if (match && active) {
          setHandoverId(match.id)
          setStatus(match.status)
          const detail = await apiClient.get<HandoverDetail>(`/handovers/${match.id}`)
          if (active && detail.data.condition) {
            setOdo(String(detail.data.condition.odo ?? ''))
            setFuel(String(detail.data.condition.fuelLevel ?? 100))
            setDamageNote(detail.data.condition.damageNote ?? '')
          }
        }
      } catch { if (active) setError('Unable to load the inspection. You can still start a new one.') }
      finally { if (active) setLoading(false) }
    }
    if (bookingId) void load()
    return () => { active = false }
  }, [bookingId, handoverType])

  const createHandover = async () => {
    setPending(true)
    setError('')
    try {
      const { data } = await apiClient.post<HandoverDetail>('/handovers/', { bookingId, type: handoverType })
      setHandoverId(data.id)
      setStatus(data.status)
      setOdo(String(data.condition?.odo ?? ''))
      setFuel(String(data.condition?.fuelLevel ?? 100))
      toast.success('Inspection started.')
    } catch { setError('Could not start the inspection. Check your connection and try again.') }
    finally { setPending(false) }
  }

  const saveAndConfirm = async () => {
    const odometer = Number(odo)
    const fuelLevel = Number(fuel)
    if (!handoverId) return setError('Start the inspection before confirming it.')
    if (!Number.isFinite(odometer) || odometer < 0) return setError('Odometer reading must be a non-negative number.')
    if (!Number.isFinite(fuelLevel) || fuelLevel < 0 || fuelLevel > 100) return setError('Fuel level must be between 0 and 100%.')
    setPending(true)
    setError('')
    try {
      await apiClient.put(`/handovers/${handoverId}/condition`, { odo: odometer, fuelLevel, damageNote: damageNote.trim() || null })
      await apiClient.put(`/handovers/${handoverId}/confirm`, { confirmedByRole: 'Customer', signatureToken: null })
      setStatus('Confirmed')
      toast.success('Handover confirmation submitted.')
    } catch { setError('Could not submit your confirmation. Please try again.') }
    finally { setPending(false) }
  }

  const setPhoto = (slot: string, file?: File) => {
    if (!file) return
    const reader = new FileReader()
    reader.onload = () => setPhotos((current) => ({ ...current, [slot]: String(reader.result ?? '') }))
    reader.readAsDataURL(file)
  }

  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={`/bookings/${bookingId}`} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Trip details</Link>
      <div className="flex items-start justify-between gap-3"><div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Vehicle handover</p><h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">{title}</h1><p className="mt-1 text-sm text-muted-foreground">Record the vehicle's condition with the owner before completing the handover.</p></div>{status && <span className={`shrink-0 rounded-full px-2.5 py-1 text-[10px] font-bold ${status.toLowerCase() === 'confirmed' ? 'bg-emerald-500/10 text-emerald-700' : 'bg-amber-500/10 text-amber-700'}`}>{status === 'Confirmed' ? 'Confirmed' : 'Draft'}</span>}</div>
      <section className="rounded-2xl border border-border bg-card p-4"><h2 className="text-sm font-bold text-foreground">Trip information</h2><p className="mt-1 font-mono text-xs text-muted-foreground">{bookingId}</p><div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground"><ShieldCheck size={15} className="text-[#1a8a9d]" /> Both parties should inspect and confirm the vehicle's condition.</div></section>
      {loading ? <div className="h-24 animate-pulse rounded-2xl bg-muted" /> : !handoverId ? <button onClick={() => void createHandover()} disabled={pending} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485] disabled:opacity-50">{pending && <LoaderCircle size={16} className="animate-spin" />}Start inspection</button> : <>
        <section className="space-y-4 rounded-2xl border border-border bg-card p-4"><h2 className="text-sm font-bold text-foreground">Vehicle condition</h2><label className="block space-y-1.5"><span className="flex items-center gap-1.5 text-xs font-semibold text-foreground"><Gauge size={15} className="text-[#1a8a9d]" />Odometer reading</span><input type="number" min="0" inputMode="numeric" value={odo} onChange={(event) => setOdo(event.target.value)} className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d]" placeholder="e.g. 15420" /></label><label className="block space-y-1.5"><span className="flex items-center gap-1.5 text-xs font-semibold text-foreground"><Fuel size={15} className="text-[#1a8a9d]" />Fuel level (%)</span><input type="number" min="0" max="100" inputMode="numeric" value={fuel} onChange={(event) => setFuel(event.target.value)} className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d]" /></label><label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">Damage or notes</span><textarea value={damageNote} onChange={(event) => setDamageNote(event.target.value)} rows={3} maxLength={500} placeholder="Describe any vehicle issues" className="w-full resize-y rounded-xl border border-border bg-background px-3 py-3 text-sm outline-none focus:border-[#1a8a9d]" /></label></section>
        <section className="space-y-3 rounded-2xl border border-border bg-card p-4"><div><h2 className="text-sm font-bold text-foreground">Condition photos</h2><p className="mt-1 text-xs text-muted-foreground">Photograph all four sides of the vehicle and the dashboard.</p></div><div className="grid grid-cols-2 gap-2">{['Front left', 'Front right', 'Rear left', 'Rear right', 'Dashboard', 'Interior'].map((slot) => <label key={slot} className="relative flex aspect-[4/3] cursor-pointer flex-col items-center justify-center overflow-hidden rounded-xl border border-dashed border-border bg-muted/30 text-center hover:border-[#1a8a9d]">{photos[slot] ? <img src={photos[slot]} alt={slot} className="absolute inset-0 size-full object-cover" /> : <><Camera size={19} className="text-[#1a8a9d]" /><span className="mt-2 px-2 text-[11px] font-semibold text-muted-foreground">{slot}</span></>}<input type="file" accept="image/*" capture="environment" className="sr-only" onChange={(event) => setPhoto(slot, event.target.files?.[0])} /></label>)}</div><p className="text-[11px] leading-5 text-muted-foreground">Photos are previewed on this device. The BE does not yet provide an image upload service.</p></section>
        <button onClick={() => void saveAndConfirm()} disabled={pending || status.toLowerCase() === 'confirmed'} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485] disabled:opacity-50">{pending ? <LoaderCircle size={16} className="animate-spin" /> : <CheckCircle2 size={17} />}{status.toLowerCase() === 'confirmed' ? 'Confirmed' : 'Save and confirm'}</button>
      </>}
      {error && <p role="alert" className="rounded-xl bg-destructive/10 p-3 text-xs text-destructive">{error}</p>}
      {handoverType === 'Return' && status.toLowerCase() === 'confirmed' && <Link to={`/bookings/${bookingId}/settlement`} className="flex h-11 w-full items-center justify-center rounded-xl border border-border bg-background text-sm font-semibold text-foreground hover:bg-muted">View settlement and deposit refund</Link>}
    </main>
  )
}

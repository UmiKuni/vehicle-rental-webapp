import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { ArrowLeft, Camera, CheckCircle2, Fuel, Gauge, LoaderCircle, ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'
import apiClient from '@/shared/lib/axios'
import { useAuth } from '@/shared/contexts/AuthContext'
import { resolvePrivateStorageUrl } from '@/shared/lib/storage-url'
import { ROUTES } from '@/shared/constants'

type HandoverType = 'Pickup' | 'Return'
type PhotoCorner = 'FrontLeft' | 'FrontRight' | 'RearLeft' | 'RearRight' | 'Dash'
interface HandoverSummary { id: string; type: string; status: string }
interface HandoverPhoto { id: string; corner: PhotoCorner; viewUrl: string }
interface HandoverDetail {
  id: string
  status: string
  condition?: { odo: number; fuelLevel: number; damageNote?: string | null }
  photos: HandoverPhoto[]
  isConfirmedByOwner: boolean
  isConfirmedByCustomer: boolean
}

const PHOTO_SLOTS: { corner: PhotoCorner; label: string }[] = [
  { corner: 'FrontLeft', label: 'Front left' },
  { corner: 'FrontRight', label: 'Front right' },
  { corner: 'RearLeft', label: 'Rear left' },
  { corner: 'RearRight', label: 'Rear right' },
  { corner: 'Dash', label: 'Dashboard' },
]

export default function HandoverPage() {
  const { bookingId = '', type = 'pickup' } = useParams()
  const location = useLocation()
  const { user } = useAuth()
  const isOwner = user?.role === 'OWNER'
  const backTo = isOwner ? ROUTES.OWNER_BOOKINGS : `/bookings/${bookingId}`
  const handoverType: HandoverType = (type || location.pathname.split('/').pop() || 'pickup').toLowerCase() === 'return' ? 'Return' : 'Pickup'
  const title = handoverType === 'Pickup' ? 'Pickup inspection' : 'Return inspection'
  const [handoverId, setHandoverId] = useState('')
  const [detail, setDetail] = useState<HandoverDetail | null>(null)
  const [odo, setOdo] = useState('')
  const [fuel, setFuel] = useState('100')
  const [damageNote, setDamageNote] = useState('')
  const [pending, setPending] = useState(false)
  const [uploadingCorner, setUploadingCorner] = useState<PhotoCorner | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [error, setError] = useState('')

  const applyDetail = (value: HandoverDetail) => {
    setHandoverId(value.id)
    setDetail(value)
    if (value.condition) {
      setOdo(String(value.condition.odo ?? ''))
      setFuel(String(value.condition.fuelLevel ?? 100))
      setDamageNote(value.condition.damageNote ?? '')
    }
  }

  const load = async () => {
    setLoadError('')
    try {
      const { data } = await apiClient.get<HandoverSummary[]>(`/handovers/booking/${bookingId}`)
      const match = data.find((item) => item.type.toLowerCase() === handoverType.toLowerCase())
      if (match) {
        const result = await apiClient.get<HandoverDetail>(`/handovers/${match.id}`)
        applyDetail(result.data)
      } else {
        setHandoverId('')
        setDetail(null)
      }
    } catch {
      setLoadError('Unable to load this inspection. Check your connection and retry.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    const run = async () => {
      setLoading(true)
      try {
        const { data } = await apiClient.get<HandoverSummary[]>(`/handovers/booking/${bookingId}`)
        const match = data.find((item) => item.type.toLowerCase() === handoverType.toLowerCase())
        if (match) {
          const result = await apiClient.get<HandoverDetail>(`/handovers/${match.id}`)
          if (active) applyDetail(result.data)
        } else if (active) {
          setHandoverId('')
          setDetail(null)
        }
      } catch {
        if (active) setLoadError('Unable to load this inspection. Check your connection and retry.')
      } finally {
        if (active) setLoading(false)
      }
    }
    if (bookingId) void run()
    return () => { active = false }
  }, [bookingId, handoverType])

  const createHandover = async () => {
    setPending(true)
    setError('')
    try {
      const { data } = await apiClient.post<HandoverDetail>('/handovers/', { bookingId, type: handoverType })
      applyDetail(data)
      toast.success('Inspection started.')
    } catch {
      setError('Could not start the inspection. It may already exist; reload to check its status.')
    } finally {
      setPending(false)
    }
  }

  const uploadPhoto = async (corner: PhotoCorner, file?: File) => {
    if (!file || !handoverId) return
    if (!['image/jpeg', 'image/jpg', 'image/png', 'image/webp'].includes(file.type.toLowerCase())) return setError('Choose a JPG, PNG or WEBP image.')
    if (file.size > 10 * 1024 * 1024) return setError('Each photo must be 10 MB or smaller.')
    setUploadingCorner(corner)
    setError('')
    try {
      const form = new FormData()
      form.append('corner', corner)
      form.append('clientCapturedAt', new Date().toISOString())
      form.append('file', file)
      const { data } = await apiClient.post<HandoverPhoto>(`/handovers/${handoverId}/photos`, form)
      setDetail((current) => current ? {
        ...current,
        photos: [...current.photos.filter((photo) => photo.corner !== corner), data],
      } : current)
      toast.success('Inspection photo uploaded.')
    } catch {
      setError('Could not upload this photo. Please retry.')
    } finally {
      setUploadingCorner(null)
    }
  }

  const saveAndConfirm = async () => {
    const odometer = Number(odo)
    const fuelLevel = Number(fuel)
    if (!handoverId) return setError('Start the inspection before confirming it.')
    if (!odo.trim() || !fuel.trim()) return setError('Enter both the odometer reading and fuel level.')
    if (!Number.isInteger(odometer) || odometer < 0) return setError('Odometer reading must be a non-negative whole number.')
    if (!Number.isInteger(fuelLevel) || fuelLevel < 0 || fuelLevel > 100) return setError('Fuel level must be a whole number from 0 to 100%.')
    setPending(true)
    setError('')
    try {
      // The first participant's condition is the shared handover record. Once
      // they have confirmed, keep that record stable for the second signer.
      const otherPartyConfirmed = isOwner ? detail?.isConfirmedByCustomer : detail?.isConfirmedByOwner
      if (!otherPartyConfirmed) {
        await apiClient.put(`/handovers/${handoverId}/condition`, { odo: odometer, fuelLevel, damageNote: damageNote.trim() || null })
      }
      const { data } = await apiClient.put<HandoverDetail>(`/handovers/${handoverId}/confirm`, { confirmedByRole: isOwner ? 'Owner' : 'Customer', signatureToken: null })
      applyDetail(data)
      const bothConfirmed = data.isConfirmedByOwner && data.isConfirmedByCustomer
      toast.success(bothConfirmed ? 'Handover confirmed by both parties.' : `Your confirmation was submitted. Waiting for the ${isOwner ? 'customer' : 'owner'}.`)
    } catch {
      setError('Could not submit your confirmation. Check the inspection details and try again.')
    } finally {
      setPending(false)
    }
  }

  const photos = detail?.photos ?? []
  const missingPhotoSlots = PHOTO_SLOTS.filter(({ corner }) => !photos.some((photo) => photo.corner === corner))
  const isConfirmed = detail?.status.toLowerCase() === 'confirmed'
  const myConfirmation = isOwner ? detail?.isConfirmedByOwner ?? false : detail?.isConfirmedByCustomer ?? false
  const otherConfirmation = isOwner ? detail?.isConfirmedByCustomer ?? false : detail?.isConfirmedByOwner ?? false

  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={backTo} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> {isOwner ? 'Owner bookings' : 'Trip details'}</Link>
      <div className="flex items-start justify-between gap-3"><div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Vehicle handover</p><h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">{title}</h1><p className="mt-1 text-sm text-muted-foreground">Record the vehicle's condition together before completing the handover.</p></div>{detail && <span className={`shrink-0 rounded-full px-2.5 py-1 text-[10px] font-bold ${isConfirmed ? 'bg-emerald-500/10 text-emerald-700' : 'bg-amber-500/10 text-amber-700'}`}>{isConfirmed ? 'Confirmed' : myConfirmation ? `Waiting for ${isOwner ? 'customer' : 'owner'}` : otherConfirmation ? 'Waiting for your confirmation' : 'Draft'}</span>}</div>
      <section className="rounded-2xl border border-border bg-card p-4"><h2 className="text-sm font-bold text-foreground">Trip information</h2><p className="mt-1 font-mono text-xs text-muted-foreground">{bookingId}</p><div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground"><ShieldCheck size={15} className="text-[#1a8a9d]" /> Both parties should inspect and confirm the vehicle's condition.</div></section>
      {loading ? <div className="h-24 animate-pulse rounded-2xl bg-muted" /> : loadError ? <div className="space-y-3 rounded-2xl border border-destructive/20 p-4"><p role="alert" className="text-sm text-destructive">{loadError}</p><button type="button" onClick={() => { setLoading(true); void load() }} className="text-sm font-semibold text-[#1a8a9d]">Retry</button></div> : !handoverId ? <button onClick={() => void createHandover()} disabled={pending} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485] disabled:opacity-50">{pending && <LoaderCircle size={16} className="animate-spin" />}Start inspection</button> : <>
        <section className="space-y-4 rounded-2xl border border-border bg-card p-4"><h2 className="text-sm font-bold text-foreground">Vehicle condition</h2><label className="block space-y-1.5"><span className="flex items-center gap-1.5 text-xs font-semibold text-foreground"><Gauge size={15} className="text-[#1a8a9d]" />Odometer reading</span><input type="number" min="0" step="1" inputMode="numeric" value={odo} onChange={(event) => setOdo(event.target.value)} disabled={isConfirmed || myConfirmation} className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d] disabled:opacity-60" placeholder="e.g. 15420" /></label><label className="block space-y-1.5"><span className="flex items-center gap-1.5 text-xs font-semibold text-foreground"><Fuel size={15} className="text-[#1a8a9d]" />Fuel level (%)</span><input type="number" min="0" max="100" step="1" inputMode="numeric" value={fuel} onChange={(event) => setFuel(event.target.value)} disabled={isConfirmed || myConfirmation} className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d] disabled:opacity-60" /></label><label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">Damage or notes</span><textarea value={damageNote} onChange={(event) => setDamageNote(event.target.value)} disabled={isConfirmed || myConfirmation} rows={3} maxLength={500} placeholder="Describe any vehicle issues" className="w-full resize-y rounded-xl border border-border bg-background px-3 py-3 text-sm outline-none focus:border-[#1a8a9d] disabled:opacity-60" /></label></section>
      <section className="space-y-3 rounded-2xl border border-border bg-card p-4"><div><h2 className="text-sm font-bold text-foreground">Condition photos</h2><p className="mt-1 text-xs text-muted-foreground">Upload each photo to the handover record. JPG, PNG or WEBP up to 10 MB.</p></div><div className="grid grid-cols-2 gap-2">{PHOTO_SLOTS.map(({ corner, label }) => { const photo = photos.find((item) => item.corner === corner); const isUploading = uploadingCorner === corner; return <label key={corner} className="relative flex aspect-[4/3] cursor-pointer flex-col items-center justify-center overflow-hidden rounded-xl border border-dashed border-border bg-muted/30 text-center hover:border-[#1a8a9d]">{photo?.viewUrl ? <img src={resolvePrivateStorageUrl(photo.viewUrl)} alt={label} className="absolute inset-0 size-full object-cover" /> : <><Camera size={19} className="text-[#1a8a9d}" /><span className="mt-2 px-2 text-[11px] font-semibold text-muted-foreground">{label}</span></>}{isUploading && <span className="absolute inset-0 grid place-items-center bg-black/50 text-xs font-semibold text-white">Uploading…</span>}<input type="file" accept="image/jpeg,image/jpg,image/png,image/webp" capture="environment" disabled={isConfirmed || myConfirmation || !!uploadingCorner} className="sr-only" onChange={(event) => { void uploadPhoto(corner, event.target.files?.[0]); event.target.value = '' }} /></label> })}</div></section>
        {missingPhotoSlots.length > 0 && !myConfirmation && !isConfirmed && <p className="text-xs text-muted-foreground">Upload all five required photos before confirming: {missingPhotoSlots.map((slot) => slot.label).join(', ')}.</p>}
        <button onClick={() => void saveAndConfirm()} disabled={pending || isConfirmed || myConfirmation || missingPhotoSlots.length > 0 || uploadingCorner !== null} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485] disabled:opacity-50">{pending ? <LoaderCircle size={16} className="animate-spin" /> : <CheckCircle2 size={17} />}{isConfirmed ? 'Confirmed by both parties' : myConfirmation ? `Waiting for ${isOwner ? 'customer' : 'owner'} confirmation` : `Save and confirm as ${isOwner ? 'owner' : 'customer'}`}</button>
      </>}
      {error && <p role="alert" className="rounded-xl bg-destructive/10 p-3 text-xs text-destructive">{error}</p>}
      {handoverType === 'Return' && isConfirmed && !isOwner && <Link to={`/bookings/${bookingId}/settlement`} className="flex h-11 w-full items-center justify-center rounded-xl border border-border bg-background text-sm font-semibold text-foreground hover:bg-muted">View settlement and deposit refund</Link>}
    </main>
  )
}

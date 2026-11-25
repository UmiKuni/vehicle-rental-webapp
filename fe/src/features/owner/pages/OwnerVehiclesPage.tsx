import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarDays, CarFront, Eye, EyeOff, Plus, Settings2 } from 'lucide-react'
import { toast } from 'sonner'
import { useAuth } from '@/shared/contexts/AuthContext'
import { getErrorMessage } from '@/shared/lib/query-client'
import { ownerApi } from '../api/owner.api'
import type { OwnerVehicle } from '../api/owner.api'
import { Status } from './OwnerDashboardPage'

const money = (value: number) => new Intl.NumberFormat('vi-VN').format(value)
const maxVehicleImageSize = 10 * 1024 * 1024
const supportedVehicleImageTypes = new Set(['image/jpeg', 'image/jpg', 'image/png', 'image/webp'])

export default function OwnerVehiclesPage() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [showCreate, setShowCreate] = useState(false)
  const [vehicleToComplete, setVehicleToComplete] = useState<OwnerVehicle | null>(null)
  const [vehicleCalendar, setVehicleCalendar] = useState<OwnerVehicle | null>(null)
  const vehicles = useQuery({ queryKey: ['owner', 'vehicles', user?.id], queryFn: () => ownerApi.getVehicles(String(user!.id)), enabled: !!user?.id })
  const visibility = useMutation({
    mutationFn: ({ id, visible }: { id: string; visible: boolean }) => ownerApi.setVehicleVisibility(id, visible),
    onSuccess: async () => { toast.success('Vehicle listing updated.'); await queryClient.invalidateQueries({ queryKey: ['owner', 'vehicles'] }) },
    onError: (error) => toast.error(getErrorMessage(error)),
  })

  return <div className="space-y-5">
    <div className="flex flex-wrap items-end justify-between gap-3"><div><p className="mb-1 text-xs font-bold uppercase tracking-[.16em] text-[#218276]">OWNER FLEET</p><h2 className="m-0 text-2xl font-extrabold tracking-tight">My vehicles</h2><p className="mb-0 mt-1 text-sm text-[#6b7e77]">Listings and status are loaded from the fleet API for your owner account.</p></div><button type="button" onClick={() => setShowCreate(true)} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[#20786d] px-4 text-sm font-bold text-white hover:bg-[#185f56]"><Plus size={16} /> Add vehicle</button></div>
    {vehicles.isPending ? <div className="rounded-2xl border border-[#e0e9e4] bg-white p-6 text-sm text-[#71817a]">Loading your fleet…</div> : vehicles.isError ? <div className="rounded-2xl border border-red-200 bg-red-50 p-5"><p role="alert" className="text-sm text-red-700">Could not load vehicles for this owner account.</p><button type="button" onClick={() => void vehicles.refetch()} className="mt-2 text-sm font-bold text-red-800">Retry</button></div> : vehicles.data.length === 0 ? <div className="rounded-2xl border border-dashed border-[#cbdad1] bg-white p-10 text-center"><CarFront className="mx-auto text-[#84a297]" size={28} /><h3 className="mb-0 mt-3 text-base font-bold">No vehicle listings yet</h3><p className="mb-4 mt-1 text-sm text-[#71817a]">Create a draft, set prices, upload at least three photos, then send it to the admin review queue.</p><button type="button" onClick={() => setShowCreate(true)} className="h-10 rounded-lg bg-[#20786d] px-4 text-sm font-bold text-white">Create your first listing</button></div> : <div className="grid gap-3 md:grid-cols-2">{vehicles.data.map((vehicle) => <VehicleCard key={vehicle.vehicleId} vehicle={vehicle} onConfigure={() => setVehicleToComplete(vehicle)} onCalendar={() => setVehicleCalendar(vehicle)} onVisibility={(visible) => visibility.mutate({ id: vehicle.vehicleId, visible })} busy={visibility.isPending} />)}</div>}

    {showCreate && <CreateVehicleDialog ownerId={String(user?.id ?? '')} onClose={() => setShowCreate(false)} onCreated={(vehicle) => { void queryClient.invalidateQueries({ queryKey: ['owner', 'vehicles'] }); setShowCreate(false); setVehicleToComplete(vehicle) }} />}
    {vehicleToComplete && <CompleteListingDialog vehicle={vehicleToComplete} ownerId={String(user?.id ?? '')} onClose={() => setVehicleToComplete(null)} />}
    {vehicleCalendar && <OwnerAvailabilityDialog vehicle={vehicleCalendar} onClose={() => setVehicleCalendar(null)} />}
  </div>
}

function VehicleCard({ vehicle, onConfigure, onCalendar, onVisibility, busy }: { vehicle: OwnerVehicle; onConfigure: () => void; onCalendar: () => void; onVisibility: (visible: boolean) => void; busy: boolean }) {
  const status = vehicle.status.toUpperCase()
  return <article className="rounded-2xl border border-[#e0e9e4] bg-white p-4 sm:p-5">
    <div className="flex items-start justify-between gap-3"><div className="min-w-0"><p className="m-0 truncate text-base font-extrabold">{vehicle.brandName} {vehicle.modelName}</p><p className="mb-0 mt-1 text-xs font-semibold text-[#70817a]">{vehicle.plateNumber} · {vehicle.seats} seats · {vehicle.transmission}</p></div><Status status={vehicle.status} /></div>
    <p className="mb-0 mt-3 truncate text-xs text-[#70817a]">{vehicle.address}</p>
    <div className="mt-4 flex flex-wrap gap-2 border-t border-[#edf1ee] pt-3">
      {status === 'DRAFT' && <button type="button" onClick={onConfigure} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-[#20786d] px-3 text-xs font-bold text-white"><Settings2 size={14} /> Complete listing</button>}
      {(status === 'PUBLISHED' || status === 'HIDDEN') && <button type="button" onClick={onCalendar} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-[#dce7e1] px-3 text-xs font-bold text-[#49635c]"><CalendarDays size={14} /> Availability</button>}
      {status === 'PUBLISHED' && <button type="button" disabled={busy} onClick={() => onVisibility(false)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-[#dce7e1] px-3 text-xs font-bold text-[#49635c] disabled:opacity-50"><EyeOff size={14} /> Hide listing</button>}
      {status === 'HIDDEN' && <button type="button" disabled={busy} onClick={() => onVisibility(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-[#dce7e1] px-3 text-xs font-bold text-[#49635c] disabled:opacity-50"><Eye size={14} /> Show listing</button>}
      {status === 'PENDINGAPPROVAL' && <span className="self-center text-xs text-[#71817a]">Waiting for admin review. You can’t edit this listing while it is under review.</span>}
    </div>
  </article>
}

function OwnerAvailabilityDialog({ vehicle, onClose }: { vehicle: OwnerVehicle; onClose: () => void }) {
  const queryClient = useQueryClient()
  const now = new Date()
  const from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString()
  const to = new Date(now.getFullYear(), now.getMonth() + 4, 0, 23, 59, 59).toISOString()
  const [startTime, setStartTime] = useState('')
  const [endTime, setEndTime] = useState('')
  const [reason, setReason] = useState('')
  const [type, setType] = useState<'OwnerBlocked' | 'Maintenance'>('OwnerBlocked')
  const key = ['owner', 'calendar', vehicle.vehicleId, from, to] as const
  const calendar = useQuery({ queryKey: key, queryFn: () => ownerApi.getCalendar(vehicle.vehicleId, from, to) })
  const block = useMutation({
    mutationFn: () => ownerApi.blockSchedule(vehicle.vehicleId, { startTime: new Date(startTime).toISOString(), endTime: new Date(endTime).toISOString(), reason: reason.trim(), type }),
    onSuccess: async () => { toast.success('Vehicle availability was blocked.'); setStartTime(''); setEndTime(''); setReason(''); await queryClient.invalidateQueries({ queryKey: key }) },
    onError: (error) => toast.error(getErrorMessage(error)),
  })
  const unblock = useMutation({
    mutationFn: ownerApi.unblockSchedule,
    onSuccess: async () => { toast.success('Schedule block removed.'); await queryClient.invalidateQueries({ queryKey: key }) },
    onError: (error) => toast.error(getErrorMessage(error)),
  })
  const valid = !!startTime && !!endTime && Number.isFinite(new Date(startTime).getTime()) && Number.isFinite(new Date(endTime).getTime()) && new Date(endTime) > new Date(startTime) && new Date(endTime) > new Date()

  return <Dialog title="Manage vehicle availability" onClose={() => block.isPending || unblock.isPending ? undefined : onClose()}>
    <p className="-mt-2 text-xs text-[#71817a]">{vehicle.brandName} {vehicle.modelName} · {vehicle.plateNumber}. Booking and hold periods are shown with owner blocks.</p>
    <form onSubmit={(event) => { event.preventDefault(); if (valid && !block.isPending) block.mutate() }} className="grid gap-3 rounded-xl bg-[#f6f9f7] p-3 sm:grid-cols-2">
      <Field label="Block starts"><input required type="datetime-local" value={startTime} onChange={(event) => setStartTime(event.target.value)} className={inputClass} /></Field>
      <Field label="Block ends"><input required type="datetime-local" value={endTime} onChange={(event) => setEndTime(event.target.value)} className={inputClass} /></Field>
      <Field label="Reason"><input value={reason} onChange={(event) => setReason(event.target.value)} maxLength={300} className={inputClass} placeholder="Personal use or scheduled service" /></Field>
      <Field label="Type"><select value={type} onChange={(event) => setType(event.target.value as 'OwnerBlocked' | 'Maintenance')} className={inputClass}><option value="OwnerBlocked">Personal use</option><option value="Maintenance">Maintenance</option></select></Field>
      <button type="submit" disabled={!valid || block.isPending || unblock.isPending} className="h-10 rounded-lg bg-[#20786d] px-4 text-xs font-bold text-white disabled:opacity-50 sm:col-span-2">{block.isPending ? 'Saving block…' : 'Block selected dates'}</button>
    </form>
    <div className="mt-4">
      <div className="flex items-center justify-between"><h4 className="m-0 text-sm font-bold">Next four months</h4>{calendar.isError && <button type="button" onClick={() => void calendar.refetch()} className="text-xs font-bold text-red-700">Retry</button>}</div>
      {calendar.isPending ? <p className="py-5 text-xs text-[#71817a]">Loading schedule…</p> : calendar.isError ? <p role="alert" className="py-4 text-xs text-red-700">Could not load the vehicle schedule.</p> : (calendar.data ?? []).length === 0 ? <p className="py-5 text-xs text-[#71817a]">No scheduled bookings or blocks in this period.</p> : <div className="mt-2 max-h-64 divide-y divide-[#edf1ee] overflow-y-auto">{[...(calendar.data ?? [])].sort((a, b) => a.startTime.localeCompare(b.startTime)).map((entry) => {
        const removable = ['OwnerBlocked', 'Maintenance'].includes(entry.type)
        return <div key={entry.id} className="flex flex-wrap items-center justify-between gap-2 py-3 text-xs"><div><p className="m-0 font-bold text-[#354743]">{entry.type === 'OwnerBlocked' ? 'Personal use' : entry.type === 'Maintenance' ? 'Maintenance' : entry.type}</p><p className="mb-0 mt-1 text-[#71817a]">{new Date(entry.startTime).toLocaleString()} – {new Date(entry.endTime).toLocaleString()}</p>{entry.note && <p className="mb-0 mt-1 text-[#71817a]">{entry.note}</p>}</div>{removable && <button type="button" disabled={unblock.isPending || block.isPending} onClick={() => unblock.mutate(entry.id)} className="rounded-md border border-[#dce7e1] px-2.5 py-1.5 text-[10px] font-bold text-[#49635c] disabled:opacity-50">Remove block</button>}</div>
      })}</div>}
    </div>
    <div className="mt-4 flex justify-end"><button type="button" disabled={block.isPending || unblock.isPending} onClick={onClose} className="h-10 rounded-lg border border-[#dce7e1] px-4 text-sm font-semibold">Close</button></div>
  </Dialog>
}

function CreateVehicleDialog({ ownerId, onClose, onCreated }: { ownerId: string; onClose: () => void; onCreated: (vehicle: OwnerVehicle) => void }) {
  const [brandId, setBrandId] = useState('')
  const [modelId, setModelId] = useState('')
  const [plateNumber, setPlateNumber] = useState('')
  const [transmission, setTransmission] = useState<'Auto' | 'Manual'>('Auto')
  const [seats, setSeats] = useState(5)
  const [latitude, setLatitude] = useState('')
  const [longitude, setLongitude] = useState('')
  const [address, setAddress] = useState('')
  const brands = useQuery({ queryKey: ['owner', 'fleet-brands'], queryFn: ownerApi.getBrands })
  const models = useQuery({ queryKey: ['owner', 'fleet-models', brandId], queryFn: () => ownerApi.getModels(brandId), enabled: !!brandId })
  const create = useMutation({
    mutationFn: () => ownerApi.createVehicle({ ownerId, plateNumber: plateNumber.trim(), brandId, modelId, transmission, seats, latitude: Number(latitude), longitude: Number(longitude), address: address.trim() }),
    onSuccess: (vehicle) => onCreated(vehicle),
    onError: (error) => toast.error(getErrorMessage(error)),
  })
  const valid = !!ownerId && !!brandId && !!modelId && !!plateNumber.trim() && !!address.trim() && Number.isInteger(seats) && seats >= 1 && seats <= 9 && latitude !== '' && longitude !== '' && Number.isFinite(Number(latitude)) && Number.isFinite(Number(longitude)) && Number(latitude) >= -90 && Number(latitude) <= 90 && Number(longitude) >= -180 && Number(longitude) <= 180

  return <Dialog title="Create vehicle draft" onClose={onClose}>
    <p className="-mt-2 text-xs leading-5 text-[#71817a]">The backend creates the vehicle as Draft. You’ll configure the rental prices and upload at least three photos in the next step before submitting it for review.</p>
    {brands.isError && <p role="alert" className="text-xs text-red-700">Could not load vehicle brands. <button type="button" onClick={() => void brands.refetch()} className="underline">Retry</button></p>}
    <form onSubmit={(event) => { event.preventDefault(); if (valid) create.mutate() }} className="grid gap-3 sm:grid-cols-2">
      <Field label="Brand"><select required value={brandId} onChange={(event) => { setBrandId(event.target.value); setModelId('') }} className={inputClass}><option value="">Select brand</option>{(brands.data ?? []).map((brand) => <option key={brand.brandId} value={brand.brandId}>{brand.name}</option>)}</select></Field>
      <Field label="Model"><select required value={modelId} onChange={(event) => setModelId(event.target.value)} disabled={!brandId || models.isPending} className={inputClass}><option value="">{models.isPending ? 'Loading…' : 'Select model'}</option>{(models.data ?? []).map((model) => <option key={model.modelId} value={model.modelId}>{model.name}</option>)}</select></Field>
      <Field label="License plate"><input required value={plateNumber} onChange={(event) => setPlateNumber(event.target.value.toUpperCase())} maxLength={15} className={inputClass} placeholder="51A-123.45" /></Field>
      <Field label="Transmission"><select value={transmission} onChange={(event) => setTransmission(event.target.value as 'Auto' | 'Manual')} className={inputClass}><option value="Auto">Automatic</option><option value="Manual">Manual</option></select></Field>
      <Field label="Seats"><input required type="number" min={1} max={9} value={seats} onChange={(event) => setSeats(Number(event.target.value))} className={inputClass} /></Field>
      <Field label="Address"><input required value={address} onChange={(event) => setAddress(event.target.value)} maxLength={500} className={inputClass} /></Field>
      <Field label="Latitude"><input required type="number" min={-90} max={90} step="any" value={latitude} onChange={(event) => setLatitude(event.target.value)} className={inputClass} placeholder="10.78" /></Field>
      <Field label="Longitude"><input required type="number" min={-180} max={180} step="any" value={longitude} onChange={(event) => setLongitude(event.target.value)} className={inputClass} placeholder="106.70" /></Field>
      <div className="flex justify-end gap-2 pt-2 sm:col-span-2"><button type="button" onClick={onClose} className="h-10 rounded-lg border border-[#dce7e1] px-4 text-sm font-semibold">Cancel</button><button type="submit" disabled={!valid || create.isPending || brands.isPending || models.isPending} className="h-10 rounded-lg bg-[#20786d] px-4 text-sm font-bold text-white disabled:opacity-50">{create.isPending ? 'Creating…' : 'Create draft'}</button></div>
    </form>
  </Dialog>
}

function CompleteListingDialog({ vehicle, ownerId, onClose }: { vehicle: OwnerVehicle; ownerId: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [weekday, setWeekday] = useState('')
  const [weekend, setWeekend] = useState('')
  const [deposit, setDeposit] = useState('')
  const [kmLimit, setKmLimit] = useState('300')
  const [files, setFiles] = useState<File[]>([])
  const [pricesSaved, setPricesSaved] = useState(false)
  const [uploadedCount, setUploadedCount] = useState(0)
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const invalidFiles = files.some((file) => !supportedVehicleImageTypes.has(file.type.toLowerCase()) || file.size > maxVehicleImageSize)
  const canContinue = [weekday, weekend, deposit, kmLimit].every((value) => value !== '' && Number.isSafeInteger(Number(value))) && Number(weekday) > 0 && Number(weekend) > 0 && Number(deposit) >= 0 && Number(kmLimit) > 0 && files.length >= 3 && !invalidFiles

  const finish = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!canContinue || pending) return
    setPending(true)
    setError('')
    try {
      if (!pricesSaved) {
        await ownerApi.configurePricing(vehicle.vehicleId, ownerId, { weekday: Number(weekday), weekend: Number(weekend), deposit: Number(deposit), kmLimit: Number(kmLimit) })
        setPricesSaved(true)
      }
      for (let index = uploadedCount; index < files.length; index += 1) {
        await ownerApi.uploadVehicleImage(vehicle.vehicleId, files[index])
        setUploadedCount(index + 1)
      }
      await ownerApi.submitVehicle(vehicle.vehicleId)
      toast.success('Listing submitted for admin review.')
      await queryClient.invalidateQueries({ queryKey: ['owner', 'vehicles'] })
      onClose()
    } catch (requestError) {
      setError(`${getErrorMessage(requestError)} The vehicle remains a draft; retry here after correcting the issue.`)
      await queryClient.invalidateQueries({ queryKey: ['owner', 'vehicles'] })
    } finally {
      setPending(false)
    }
  }

  return <Dialog title="Complete vehicle listing" onClose={pending ? () => undefined : onClose}>
    <p className="-mt-2 text-xs text-[#71817a]">{vehicle.brandName} {vehicle.modelName} · {vehicle.plateNumber}. Set the rates and upload three or more photos. The first uploaded image becomes the primary photo.</p>
    <form onSubmit={(event) => void finish(event)} className="space-y-4">
      <div className="grid grid-cols-2 gap-3"><Field label="Weekday rate (VND/day)"><input required type="number" min={1} step={10000} value={weekday} onChange={(event) => { setWeekday(event.target.value); setPricesSaved(false) }} className={inputClass} /></Field><Field label="Weekend rate (VND/day)"><input required type="number" min={1} step={10000} value={weekend} onChange={(event) => { setWeekend(event.target.value); setPricesSaved(false) }} className={inputClass} /></Field><Field label="Security deposit (VND)"><input required type="number" min={0} step={10000} value={deposit} onChange={(event) => { setDeposit(event.target.value); setPricesSaved(false) }} className={inputClass} /></Field><Field label="Included km/day"><input required type="number" min={1} value={kmLimit} onChange={(event) => { setKmLimit(event.target.value); setPricesSaved(false) }} className={inputClass} /></Field></div>
      <label className="block text-xs font-semibold text-[#52655d]">Vehicle photos (minimum 3; JPG, PNG or WEBP up to 10 MB each)<input type="file" accept="image/jpeg,image/jpg,image/png,image/webp" multiple disabled={pending || uploadedCount > 0} onChange={(event) => { setFiles(Array.from(event.target.files ?? [])) }} className="mt-1.5 block w-full rounded-lg border border-[#dce7e1] bg-white p-2 text-xs disabled:opacity-50" /></label>
      {files.length > 0 && <p className="-mt-3 text-[11px] text-[#71817a]">{files.length} selected · {uploadedCount} uploaded</p>}
      {invalidFiles && <p role="alert" className="-mt-3 text-xs text-red-700">Use JPG, PNG or WEBP images. Each file must be no larger than 10 MB.</p>}
      {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-xs text-red-800">{error}</p>}
      <p className="m-0 rounded-lg bg-amber-50 p-3 text-[11px] leading-5 text-amber-900">These steps call separate backend APIs; if a step fails, the vehicle stays in Draft. Upload progress is kept while this dialog remains open so retry does not reupload completed files. Keep the same photo selection after an upload succeeds; changing it could upload duplicates.</p>
      <div className="flex justify-end gap-2"><button type="button" disabled={pending} onClick={onClose} className="h-10 rounded-lg border border-[#dce7e1] px-4 text-sm font-semibold disabled:opacity-50">Close</button><button type="submit" disabled={!canContinue || pending} className="h-10 rounded-lg bg-[#20786d] px-4 text-sm font-bold text-white disabled:opacity-50">{pending ? 'Saving listing…' : 'Submit for review'}</button></div>
      {pricesSaved && <p className="m-0 text-[10px] text-[#71817a]">Configured weekday {money(Number(weekday))} ₫, weekend {money(Number(weekend))} ₫, deposit {money(Number(deposit))} ₫.</p>}
    </form>
  </Dialog>
}

const inputClass = 'h-10 w-full rounded-lg border border-[#dce7e1] bg-white px-3 text-sm outline-none focus:border-[#73a99a]'
function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="block text-xs font-semibold text-[#52655d]">{label}<span className="mt-1.5 block">{children}</span></label> }
function Dialog({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) { return <div className="fixed inset-0 z-50 grid place-items-center bg-black/45 p-4" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}><section role="dialog" aria-modal="true" aria-labelledby="owner-dialog-title" className="max-h-[92dvh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-5 shadow-2xl sm:p-6"><h3 id="owner-dialog-title" className="mb-4 mt-0 text-xl font-extrabold">{title}</h3>{children}</section></div> }

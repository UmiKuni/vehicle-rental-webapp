import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft, ArrowRight, FileImage, ShieldCheck } from 'lucide-react'
import { ROUTES } from '@/shared/constants'
import { ImageUploadZone } from '../components/ImageUploadZone'

const draftKey = 'customer-verification-draft'

export default function IdentityVerificationPage() {
  const navigate = useNavigate()
  const [docType, setDocType] = useState<'CCCD' | 'PASSPORT'>('CCCD')
  const [idNumber, setIdNumber] = useState('')
  const [country, setCountry] = useState('Vietnam')
  const [expiryDate, setExpiryDate] = useState('')
  const [front, setFront] = useState('')
  const [back, setBack] = useState('')
  const [error, setError] = useState('')

  const preview = (file: File, setter: (value: string) => void) => {
    const reader = new FileReader()
    reader.onload = () => setter(String(reader.result ?? ''))
    reader.readAsDataURL(file)
  }

  const handleContinue = () => {
    const value = idNumber.trim().toUpperCase()
    if (!value) return setError('Enter your document number.')
    if (docType === 'CCCD' && !/^\d{9,12}$/.test(value)) return setError('Your National ID number must contain 9 to 12 digits.')
    if (docType === 'PASSPORT' && !/^[A-Z0-9]{6,12}$/.test(value)) return setError('Your passport number must contain 6 to 12 characters.')
    if (!front) return setError('Upload the front of your ID or the information page of your passport.')
    if (docType === 'CCCD' && !back) return setError('Upload the back of your National ID.')
    if (docType === 'PASSPORT' && !expiryDate) return setError('Enter your passport expiry date.')
    const previous = JSON.parse(sessionStorage.getItem(draftKey) ?? '{}') as Record<string, unknown>
    sessionStorage.setItem(draftKey, JSON.stringify({ ...previous, identity: { docType, idNumber: value, country, expiryDate } }))
    navigate(ROUTES.VERIFY_LICENSE)
  }

  return (
    <main className="mx-auto w-full max-w-md space-y-5 px-4 pt-5 pb-28">
      <button onClick={() => navigate(ROUTES.PROFILE_VERIFY)} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Verification instructions</button>
      <div className="flex items-start gap-3"><div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><ShieldCheck size={22} /></div><div><p className="text-xs font-semibold text-[#1a8a9d]">Step 1 of 2</p><h1 className="mt-1 text-xl font-bold text-foreground">Identity document</h1><p className="mt-1 text-xs leading-5 text-muted-foreground">Use clear photos with all edges visible and no glare.</p></div></div>
      <div className="grid grid-cols-2 gap-2 rounded-xl bg-muted/60 p-1">
        {(['CCCD', 'PASSPORT'] as const).map((type) => <button key={type} type="button" onClick={() => { setDocType(type); setError('') }} className={`h-10 rounded-lg text-xs font-bold transition ${docType === type ? 'bg-background text-[#1a8a9d] shadow-sm' : 'text-muted-foreground'}`}>{type === 'CCCD' ? 'National ID' : 'Passport'}</button>)}
      </div>
      <label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">{docType === 'CCCD' ? 'National ID number' : 'Passport number'}</span><input className="h-12 w-full rounded-xl border border-border bg-background px-3 font-mono text-sm uppercase outline-none focus:border-[#1a8a9d] focus:ring-2 focus:ring-[#1a8a9d]/15" value={idNumber} onChange={(event) => setIdNumber(event.target.value)} autoComplete="off" /></label>
      {docType === 'PASSPORT' && <div className="grid grid-cols-2 gap-3"><label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">Nationality</span><input className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d]" value={country} onChange={(event) => setCountry(event.target.value)} /></label><label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">Expiry date</span><input type="date" min={new Date().toISOString().slice(0, 10)} className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d]" value={expiryDate} onChange={(event) => setExpiryDate(event.target.value)} /></label></div>}
      <div className="space-y-3"><ImageUploadZone label={docType === 'CCCD' ? 'National ID front' : 'Passport information page'} preview={front} onFileSelect={(file) => preview(file, setFront)} isUploading={false} hint="Choose a photo or take one" />{docType === 'CCCD' && <ImageUploadZone label="National ID back" preview={back} onFileSelect={(file) => preview(file, setBack)} isUploading={false} hint="Choose a photo or take one" />}</div>
      {error && <p role="alert" className="rounded-lg bg-destructive/10 p-3 text-xs text-destructive">{error}</p>}
      <div className="rounded-xl border border-border bg-muted/35 p-3 text-xs leading-5 text-muted-foreground"><FileImage size={15} className="mr-1 inline text-[#1a8a9d]" />Photos are previewed on this device. Server uploads will be available when the verification API is ready.</div>
      <button onClick={handleContinue} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485]">Continue <ArrowRight size={17} /></button>
    </main>
  )
}

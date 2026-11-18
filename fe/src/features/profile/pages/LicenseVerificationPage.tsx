import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, ShieldCheck } from 'lucide-react'
import { ROUTES } from '@/shared/constants'
import { ImageUploadZone } from '../components/ImageUploadZone'
import type { LicenseClass } from '@/types/kyc.types'
import type { KycSubmitPayload } from '@/types/kyc.types'
import { useKycSubmit } from '../hooks/useKycSubmit'

const draftKey = 'customer-verification-draft'
const licenseClasses: LicenseClass[] = ['A1', 'A2', 'B1', 'B2', 'C', 'D', 'E', 'FB', 'FC', 'FD', 'FE', 'INTERNATIONAL']

export default function LicenseVerificationPage() {
  const navigate = useNavigate()
  const [licenseClass, setLicenseClass] = useState<LicenseClass>('B2')
  const [licenseNumber, setLicenseNumber] = useState('')
  const [front, setFront] = useState('')
  const [back, setBack] = useState('')
  const [error, setError] = useState('')
  const { mutate: submitKyc, isPending, isSuccess } = useKycSubmit()

  const preview = (file: File, setter: (value: string) => void) => {
    const reader = new FileReader()
    reader.onload = () => setter(String(reader.result ?? ''))
    reader.readAsDataURL(file)
  }

  const handleSave = () => {
    if (!licenseNumber.trim()) return setError("Enter your driver's license number.")
    if (!front || !back) return setError("Upload photos of both sides of your driver's license.")
    try {
      const previous = JSON.parse(sessionStorage.getItem(draftKey) ?? '{}') as { identity?: { docType?: 'CCCD' | 'PASSPORT'; idNumber?: string } }
      if (!previous.identity?.idNumber) return setError('Complete the identity document step first.')
      sessionStorage.setItem(draftKey, JSON.stringify({ ...previous, license: { licenseClass, licenseNumber: licenseNumber.trim(), hasFrontPreview: true, hasBackPreview: true } }))
      const payload: KycSubmitPayload = {
        step1: { vehicleInterests: [], licenseClass, hasLicense: true },
        licenseNumber: licenseNumber.trim(),
        step2: { docType: previous.identity.docType ?? 'CCCD', idNumber: previous.identity.idNumber },
        step3: { dateOfBirth: '', address: '', province: '', emergencyContact: '', emergencyPhone: '' },
      }
      submitKyc(payload)
    } catch {
      setError('Could not read the saved verification details. Please restart the verification steps.')
    }
  }

  return (
    <main className="mx-auto w-full max-w-md space-y-5 px-4 pt-5 pb-28">
      <button onClick={() => navigate(ROUTES.VERIFY_IDENTITY)} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Identity document</button>
      <div className="flex items-start gap-3"><div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><ShieldCheck size={22} /></div><div><p className="text-xs font-semibold text-[#1a8a9d]">Step 2 of 2</p><h1 className="mt-1 text-xl font-bold text-foreground">Driver's license</h1><p className="mt-1 text-xs leading-5 text-muted-foreground">Your license details will be checked before vehicle pickup.</p></div></div>
      <label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">License class</span><select value={licenseClass} onChange={(event) => setLicenseClass(event.target.value as LicenseClass)} className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d]">{licenseClasses.map((value) => <option key={value} value={value}>{value === 'INTERNATIONAL' ? 'International Driving Permit' : `Class ${value}`}</option>)}</select></label>
      <label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">License number</span><input value={licenseNumber} onChange={(event) => setLicenseNumber(event.target.value)} className="h-12 w-full rounded-xl border border-border bg-background px-3 font-mono text-sm outline-none focus:border-[#1a8a9d]" autoComplete="off" /></label>
      <div className="space-y-3"><ImageUploadZone label="License front" preview={front} onFileSelect={(file) => preview(file, setFront)} isUploading={false} hint="Choose a photo or take one" /><ImageUploadZone label="License back" preview={back} onFileSelect={(file) => preview(file, setBack)} isUploading={false} hint="Choose a photo or take one" /></div>
      <div className="rounded-xl border border-border bg-muted/35 p-3 text-xs leading-5 text-muted-foreground">Your details are sent to the backend mock verification endpoints. Document photos stay on this device because the backend has no image upload endpoint.</div>
      {error && <p role="alert" className="rounded-lg bg-destructive/10 p-3 text-xs text-destructive">{error}</p>}
      {isSuccess ? <div className="space-y-3 rounded-2xl border border-emerald-500/25 bg-emerald-500/5 p-4"><div className="flex items-center gap-2 text-sm font-bold text-emerald-700 dark:text-emerald-400"><CheckCircle2 size={18} /> Verification accepted</div><p className="text-xs leading-5 text-muted-foreground">The backend mock verification API accepted your ID and driver's license details.</p><Link to={ROUTES.PROFILE} className="inline-flex h-10 w-full items-center justify-center rounded-xl bg-[#1a8a9d] text-sm font-bold text-white">Back to profile</Link></div> : <button onClick={handleSave} disabled={isPending} className="h-12 w-full rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485] disabled:opacity-60">{isPending ? 'Submitting…' : 'Submit verification'}</button>}
    </main>
  )
}

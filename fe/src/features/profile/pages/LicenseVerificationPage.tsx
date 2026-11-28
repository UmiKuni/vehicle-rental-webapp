import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, ShieldCheck } from 'lucide-react'
import { ROUTES } from '@/shared/constants'
import { ImageUploadZone } from '../components/ImageUploadZone'
import type { LicenseClass, KycSubmitPayload } from '@/types/kyc.types'
import { useKycSubmit } from '../hooks/useKycSubmit'
import { getErrorMessage } from '@/shared/lib/query-client'

interface IdentityFiles { idFrontFile?: File; idBackFile?: File }
const licenseClasses: LicenseClass[] = ['A1', 'A2', 'A', 'B1', 'B2', 'B', 'C', 'D1', 'D2', 'D', 'E', 'FC', 'INTERNATIONAL']

export default function LicenseVerificationPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const identityFiles = (location.state as IdentityFiles | null) ?? {}
  const [licenseClass, setLicenseClass] = useState<LicenseClass>('B2')
  const [licenseNumber, setLicenseNumber] = useState('')
  const [frontFile, setFrontFile] = useState<File>()
  const [backFile, setBackFile] = useState<File>()
  const [front, setFront] = useState('')
  const [back, setBack] = useState('')
  const [error, setError] = useState('')
  const { mutateAsync: submitKyc, isPending, isSuccess } = useKycSubmit()

  const selectImage = (file: File, side: 'front' | 'back') => {
    const isPdf = file.type.toLowerCase() === 'application/pdf'
    if (!['image/jpeg', 'image/jpg', 'image/png', 'image/webp'].includes(file.type.toLowerCase()) && !(side === 'front' && isPdf)) return setError('Choose a JPG, PNG, WEBP or one combined PDF document.')
    if (side === 'back' && frontFile?.type.toLowerCase() === 'application/pdf') return setError('A PDF already contains both sides. Remove it before adding a back image.')
    if (file.size > 10 * 1024 * 1024) return setError('Each license file must be 10 MB or smaller.')
    const url = URL.createObjectURL(file)
    if (side === 'front') { setFrontFile(file); setFront(url); if (isPdf) { setBackFile(undefined); setBack('') } }
    else { setBackFile(file); setBack(url) }
    setError('')
  }

  const handleSave = async () => {
    const hasIdentityPdf = identityFiles.idFrontFile?.type.toLowerCase() === 'application/pdf'
    if (!identityFiles.idFrontFile || (!hasIdentityPdf && !identityFiles.idBackFile)) return setError('Start again from the National ID step and upload one PDF or both ID images.')
    const validLicenseNumber = licenseClass === 'INTERNATIONAL'
      ? /^[A-Za-z0-9-]{5,30}$/.test(licenseNumber.trim())
      : /^\d{12}$/.test(licenseNumber.trim())
    if (!validLicenseNumber) {
      return setError(licenseClass === 'INTERNATIONAL'
        ? 'Enter a valid international permit number (5–30 letters, digits or hyphens).'
        : "Driver's license number must contain exactly 12 digits.")
    }
    if (!frontFile || (frontFile.type.toLowerCase() !== 'application/pdf' && !backFile)) return setError("Upload one combined PDF or both sides of your driver's license as images.")
    const payload: KycSubmitPayload = {
      step1: { vehicleInterests: [], licenseClass, hasLicense: true },
      licenseNumber: licenseNumber.trim(),
      licenseFrontImage: frontFile,
      licenseBackImage: backFile,
      step2: {
        docType: 'CCCD',
        idNumber: (() => {
          try { return (JSON.parse(sessionStorage.getItem('customer-verification-draft') ?? '{}') as { identity?: { idNumber?: string } }).identity?.idNumber ?? '' }
          catch { return '' }
        })(),
        frontImage: identityFiles.idFrontFile,
        backImage: identityFiles.idBackFile,
      },
      step3: { dateOfBirth: '', address: '', province: '', emergencyContact: '', emergencyPhone: '' },
    }
    if (!payload.step2.idNumber) return setError('National ID number was not saved. Restart the verification steps.')
    setError('')
    try { await submitKyc(payload) }
    catch (submitError) { setError(getErrorMessage(submitError)) }
  }

  if (isSuccess) return (
    <main className="mx-auto flex min-h-[60vh] w-full max-w-md flex-col justify-center space-y-4 px-4 text-center">
      <div className="mx-auto grid size-16 place-items-center rounded-2xl bg-emerald-500/10 text-emerald-600"><CheckCircle2 size={30} /></div>
      <h1 className="text-xl font-bold text-foreground">Documents submitted</h1>
      <p className="text-sm leading-6 text-muted-foreground">Your National ID and driver's license are waiting for an administrator to review them. You can check each document's status from your profile.</p>
      <Link to={ROUTES.PROFILE} className="flex h-11 items-center justify-center rounded-xl bg-[#1a8a9d] text-sm font-bold text-white">Back to profile</Link>
    </main>
  )

  return (
    <main className="mx-auto w-full max-w-md space-y-5 px-4 pt-5 pb-28">
      <button onClick={() => navigate(ROUTES.VERIFY_IDENTITY)} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> National ID</button>
      <div className="flex items-start gap-3"><div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><ShieldCheck size={22} /></div><div><p className="text-xs font-semibold text-[#1a8a9d]">Step 2 of 2</p><h1 className="mt-1 text-xl font-bold text-foreground">Driver's license</h1><p className="mt-1 text-xs leading-5 text-muted-foreground">Both sides are uploaded to the backend with your verification request.</p></div></div>
      <label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">License class</span><select value={licenseClass} onChange={(event) => setLicenseClass(event.target.value as LicenseClass)} className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d]">{licenseClasses.map((value) => <option key={value} value={value}>{value === 'INTERNATIONAL' ? 'International Driving Permit' : `Class ${value}`}</option>)}</select></label>
      <label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">{licenseClass === 'INTERNATIONAL' ? 'Permit number' : 'License number (12 digits)'}</span><input inputMode={licenseClass === 'INTERNATIONAL' ? 'text' : 'numeric'} maxLength={licenseClass === 'INTERNATIONAL' ? 30 : 12} value={licenseNumber} onChange={(event) => setLicenseNumber(licenseClass === 'INTERNATIONAL' ? event.target.value.replace(/[^A-Za-z0-9-]/g, '') : event.target.value.replace(/\D/g, ''))} className="h-12 w-full rounded-xl border border-border bg-background px-3 font-mono text-sm outline-none focus:border-[#1a8a9d]" autoComplete="off" /></label>
      <div className="space-y-3"><ImageUploadZone label="License front or combined PDF" preview={front} selectedFile={frontFile} acceptPdf onFileSelect={(file) => selectImage(file, 'front')} isUploading={false} hint="Choose a photo or a PDF containing both sides" /><ImageUploadZone label="License back" preview={back} selectedFile={backFile} onFileSelect={(file) => selectImage(file, 'back')} isUploading={false} hint="Choose a photo or take one" /></div>
      {error && <p role="alert" className="rounded-xl bg-destructive/10 p-3 text-xs text-destructive">{error}</p>}
      <button onClick={() => void handleSave()} disabled={isPending} className="h-12 w-full rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485] disabled:opacity-60">{isPending ? 'Uploading documents…' : 'Submit documents for review'}</button>
    </main>
  )
}

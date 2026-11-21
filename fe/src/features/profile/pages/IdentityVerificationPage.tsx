import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft, ArrowRight, FileImage, ShieldCheck } from 'lucide-react'
import { ROUTES } from '@/shared/constants'
import { ImageUploadZone } from '../components/ImageUploadZone'

const draftKey = 'customer-verification-draft'

export default function IdentityVerificationPage() {
  const navigate = useNavigate()
  const [idNumber, setIdNumber] = useState('')
  const [frontFile, setFrontFile] = useState<File>()
  const [backFile, setBackFile] = useState<File>()
  const [front, setFront] = useState('')
  const [back, setBack] = useState('')
  const [error, setError] = useState('')

  const selectImage = (file: File, side: 'front' | 'back') => {
    if (!file.type.startsWith('image/')) return setError('Choose an image file.')
    if (file.size > 10 * 1024 * 1024) return setError('Each document image must be 10 MB or smaller.')
    const url = URL.createObjectURL(file)
    if (side === 'front') { setFrontFile(file); setFront(url) }
    else { setBackFile(file); setBack(url) }
    setError('')
  }

  const handleContinue = () => {
    const value = idNumber.trim()
    if (!/^\d{9,12}$/.test(value)) return setError('National ID number must contain 9 to 12 digits.')
    if (!frontFile || !backFile) return setError('Upload both sides of your National ID.')
    try {
      const previous = JSON.parse(sessionStorage.getItem(draftKey) ?? '{}') as Record<string, unknown>
      sessionStorage.setItem(draftKey, JSON.stringify({ ...previous, identity: { docType: 'CCCD', idNumber: value } }))
    } catch {
      setError('Could not save the verification draft. Enable browser storage and try again.')
      return
    }
    navigate(ROUTES.VERIFY_LICENSE, { state: { idFrontFile: frontFile, idBackFile: backFile } })
  }

  return (
    <main className="mx-auto w-full max-w-md space-y-5 px-4 pt-5 pb-28">
      <button onClick={() => navigate(ROUTES.PROFILE_VERIFY)} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Verification instructions</button>
      <div className="flex items-start gap-3"><div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><ShieldCheck size={22} /></div><div><p className="text-xs font-semibold text-[#1a8a9d]">Step 1 of 2</p><h1 className="mt-1 text-xl font-bold text-foreground">National ID (CCCD)</h1><p className="mt-1 text-xs leading-5 text-muted-foreground">The current verification endpoint supports Vietnamese National ID only.</p></div></div>
      <label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">National ID number</span><input inputMode="numeric" maxLength={12} className="h-12 w-full rounded-xl border border-border bg-background px-3 font-mono text-sm outline-none focus:border-[#1a8a9d]" value={idNumber} onChange={(event) => setIdNumber(event.target.value.replace(/\D/g, ''))} autoComplete="off" /></label>
      <div className="space-y-3"><ImageUploadZone label="National ID front" preview={front} onFileSelect={(file) => selectImage(file, 'front')} isUploading={false} hint="Choose a photo or take one" /><ImageUploadZone label="National ID back" preview={back} onFileSelect={(file) => selectImage(file, 'back')} isUploading={false} hint="Choose a photo or take one" /></div>
      {error && <p role="alert" className="rounded-lg bg-destructive/10 p-3 text-xs text-destructive">{error}</p>}
      <div className="rounded-xl border border-border bg-muted/35 p-3 text-xs leading-5 text-muted-foreground"><FileImage size={15} className="mr-1 inline text-[#1a8a9d]" />Both sides are uploaded together to private storage when you submit verification. The backend currently uses mock verification checks.</div>
      <button onClick={handleContinue} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485]">Continue <ArrowRight size={17} /></button>
    </main>
  )
}

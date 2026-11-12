import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'
import { ROUTES } from '@/shared/constants'
import { ImageUploadZone } from '../components/ImageUploadZone'
import type { LicenseClass } from '@/types/kyc.types'

const draftKey = 'customer-verification-draft'
const licenseClasses: LicenseClass[] = ['A1', 'A2', 'B1', 'B2', 'C', 'D', 'E', 'FB', 'FC', 'FD', 'FE', 'INTERNATIONAL']

export default function LicenseVerificationPage() {
  const navigate = useNavigate()
  const [licenseClass, setLicenseClass] = useState<LicenseClass>('B2')
  const [licenseNumber, setLicenseNumber] = useState('')
  const [front, setFront] = useState('')
  const [back, setBack] = useState('')
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)

  const preview = (file: File, setter: (value: string) => void) => {
    const reader = new FileReader()
    reader.onload = () => setter(String(reader.result ?? ''))
    reader.readAsDataURL(file)
  }

  const handleSave = () => {
    if (!licenseNumber.trim()) return setError('Vui lòng nhập số giấy phép lái xe.')
    if (!front || !back) return setError('Vui lòng tải ảnh cả hai mặt giấy phép lái xe.')
    const previous = JSON.parse(sessionStorage.getItem(draftKey) ?? '{}') as Record<string, unknown>
    sessionStorage.setItem(draftKey, JSON.stringify({ ...previous, license: { licenseClass, licenseNumber: licenseNumber.trim(), hasFrontPreview: true, hasBackPreview: true } }))
    setSaved(true)
    toast.success('Đã lưu thông tin xác minh trên thiết bị')
  }

  return (
    <main className="mx-auto w-full max-w-md space-y-5 px-4 pt-5 pb-28">
      <button onClick={() => navigate(ROUTES.VERIFY_IDENTITY)} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Giấy tờ tùy thân</button>
      <div className="flex items-start gap-3"><div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><ShieldCheck size={22} /></div><div><p className="text-xs font-semibold text-[#1a8a9d]">Bước 2 trong 2</p><h1 className="mt-1 text-xl font-bold text-foreground">Giấy phép lái xe</h1><p className="mt-1 text-xs leading-5 text-muted-foreground">Thông tin bằng lái dùng để đối chiếu trước khi nhận xe.</p></div></div>
      <label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">Hạng giấy phép</span><select value={licenseClass} onChange={(event) => setLicenseClass(event.target.value as LicenseClass)} className="h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none focus:border-[#1a8a9d]">{licenseClasses.map((value) => <option key={value} value={value}>{value === 'INTERNATIONAL' ? 'International Driving Permit' : `Hạng ${value}`}</option>)}</select></label>
      <label className="block space-y-1.5"><span className="text-xs font-semibold text-foreground">Số giấy phép</span><input value={licenseNumber} onChange={(event) => setLicenseNumber(event.target.value)} className="h-12 w-full rounded-xl border border-border bg-background px-3 font-mono text-sm outline-none focus:border-[#1a8a9d]" autoComplete="off" /></label>
      <div className="space-y-3"><ImageUploadZone label="Mặt trước giấy phép" preview={front} onFileSelect={(file) => preview(file, setFront)} isUploading={false} hint="Chọn ảnh hoặc chụp trực tiếp" /><ImageUploadZone label="Mặt sau giấy phép" preview={back} onFileSelect={(file) => preview(file, setBack)} isUploading={false} hint="Chọn ảnh hoặc chụp trực tiếp" /></div>
      <div className="rounded-xl border border-border bg-muted/35 p-3 text-xs leading-5 text-muted-foreground">Hồ sơ được lưu nháp trên thiết bị. BE hiện chưa có API gửi và tải ảnh bằng lái lên máy chủ.</div>
      {error && <p role="alert" className="rounded-lg bg-destructive/10 p-3 text-xs text-destructive">{error}</p>}
      {saved ? <div className="space-y-3 rounded-2xl border border-emerald-500/25 bg-emerald-500/5 p-4"><div className="flex items-center gap-2 text-sm font-bold text-emerald-700 dark:text-emerald-400"><CheckCircle2 size={18} /> Đã lưu bản nháp xác minh</div><p className="text-xs leading-5 text-muted-foreground">Thông tin đã được lưu trên thiết bị này, chưa được gửi đến hệ thống.</p><Link to={ROUTES.PROFILE} className="inline-flex h-10 w-full items-center justify-center rounded-xl bg-[#1a8a9d] text-sm font-bold text-white">Về hồ sơ</Link></div> : <button onClick={handleSave} className="h-12 w-full rounded-xl bg-[#1a8a9d] text-sm font-bold text-white hover:bg-[#147485]">Lưu bản nháp</button>}
    </main>
  )
}

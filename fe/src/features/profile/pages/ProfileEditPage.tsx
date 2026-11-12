import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, Check, LockKeyhole } from 'lucide-react'
import { toast } from 'sonner'
import { ROUTES } from '@/shared/constants'
import { useProfile, useUpdateProfile } from '../hooks/useProfile'

const fieldClass = 'h-12 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none transition focus:border-[#1a8a9d] focus:ring-2 focus:ring-[#1a8a9d]/15'

export default function ProfileEditPage() {
  const { data: profile, isLoading, isError } = useProfile()
  if (isLoading) return <main className="space-y-3 px-4 pt-5"><div className="h-8 w-48 animate-pulse rounded bg-muted" /><div className="h-12 animate-pulse rounded-xl bg-muted" /><div className="h-12 animate-pulse rounded-xl bg-muted" /></main>
  if (isError || !profile) return <main className="px-4 pt-5"><p role="alert" className="rounded-xl border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">Không tải được hồ sơ. Hãy tải lại trang và thử lại.</p></main>
  return <ProfileEditForm profile={profile} />
}

function ProfileEditForm({ profile }: { profile: NonNullable<ReturnType<typeof useProfile>['data']> }) {
  const navigate = useNavigate()
  const { mutateAsync: updateProfile, isPending } = useUpdateProfile()
  const [fullName, setFullName] = useState(profile.fullName ?? '')
  const [address, setAddress] = useState(profile.address ?? '')

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    try {
      await updateProfile({ fullName: fullName.trim(), address: address.trim() })
      toast.success('Đã cập nhật hồ sơ')
      navigate(ROUTES.PROFILE)
    } catch {
      toast.error('Không thể lưu hồ sơ. Vui lòng thử lại.')
    }
  }

  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={ROUTES.PROFILE} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Hồ sơ của tôi</Link>
      <div>
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Tài khoản</p>
        <h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">Chỉnh sửa hồ sơ</h1>
        <p className="mt-1 text-sm text-muted-foreground">Cập nhật thông tin liên hệ để việc thuê xe thuận tiện hơn.</p>
      </div>
      <form onSubmit={handleSubmit} className="space-y-4">
          <label className="block space-y-1.5">
            <span className="text-xs font-semibold text-foreground">Họ và tên</span>
            <input className={fieldClass} value={fullName} onChange={(event) => setFullName(event.target.value)} autoComplete="name" required maxLength={120} />
          </label>
          <label className="block space-y-1.5">
            <span className="text-xs font-semibold text-foreground">Địa chỉ</span>
            <input className={fieldClass} value={address} onChange={(event) => setAddress(event.target.value)} autoComplete="street-address" maxLength={240} placeholder="Số nhà, đường, phường/xã" />
          </label>
          <div className="space-y-1.5">
            <span className="text-xs font-semibold text-foreground">Email</span>
            <div className="flex h-12 items-center justify-between rounded-xl border border-border bg-muted/50 px-3 text-sm text-muted-foreground"><span className="truncate">{profile?.email}</span><LockKeyhole size={15} /></div>
            <p className="text-[11px] text-muted-foreground">Email được dùng để đăng nhập và hiện chưa thể đổi tại đây.</p>
          </div>
          <div className="space-y-1.5">
            <span className="text-xs font-semibold text-foreground">Số điện thoại</span>
            <div className="flex h-12 items-center justify-between rounded-xl border border-border bg-muted/50 px-3 text-sm text-muted-foreground"><span>{profile?.phone || 'Chưa cập nhật'}</span><LockKeyhole size={15} /></div>
          </div>
          <div className="grid grid-cols-2 gap-3 pt-2">
            <Link to={ROUTES.PROFILE} className="inline-flex h-12 items-center justify-center rounded-xl border border-border bg-card text-sm font-semibold text-foreground hover:bg-muted">Hủy</Link>
            <button disabled={isPending || !fullName.trim()} className="inline-flex h-12 items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white transition hover:bg-[#147485] disabled:opacity-50"><Check size={17} />{isPending ? 'Đang lưu…' : 'Lưu thay đổi'}</button>
          </div>
      </form>
    </main>
  )
}

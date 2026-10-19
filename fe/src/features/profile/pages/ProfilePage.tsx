import { Link } from 'react-router-dom'
import {
  ShieldAlert, ChevronRight, LogOut, User, Mail, Shield,
  CalendarDays, MapPin, AlertTriangle, Clock, Settings, CreditCard,
} from 'lucide-react'
import { useProfile, useKycStatus } from '../hooks/useProfile'
import { useAuth } from '@/shared/contexts/AuthContext'
import { VerificationBadge } from '../components/VerificationBadge'
import { KycDevTester } from '../components/KycDevTester'
import { ROUTES } from '@/shared/constants'
import { cn } from '@/shared/utils'
import { getInitials, formatDate } from '@/shared/utils'

// ─── Sub-components to keep this file under 250 lines ────────────────────────

function ProfileSkeleton() {
  return (
    <div className="animate-pulse space-y-4">
      <div className="h-32 bg-muted rounded-2xl" />
      <div className="h-20 bg-muted rounded-2xl" />
      <div className="h-40 bg-muted rounded-2xl" />
    </div>
  )
}

function KycPendingBanner() {
  return (
    <div className="mx-4 rounded-2xl border border-amber-200 bg-amber-50 dark:border-amber-800 dark:bg-amber-950/30 p-4">
      <div className="flex items-start gap-3">
        <Clock className="size-5 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
        <div>
          <p className="text-sm font-semibold text-amber-800 dark:text-amber-300">
            Đang chờ xem xét
          </p>
          <p className="mt-1 text-xs text-amber-700 dark:text-amber-400 leading-relaxed">
            Hồ sơ của bạn đã được gửi. Chúng tôi sẽ phản hồi trong 1–2 ngày làm việc.
          </p>
        </div>
      </div>
    </div>
  )
}

function KycUnverifiedCta() {
  return (
    <div className="mx-4">
      <div className="rounded-2xl overflow-hidden">
        {/* Gradient header */}
        <div className="bg-gradient-to-br from-[#0c1017] to-[#1a3040] p-5">
          <div className="flex items-center gap-3 mb-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/10">
              <ShieldAlert className="size-5 text-[#22d3ee]" />
            </div>
            <div>
              <p className="text-sm font-bold text-white">Xác thực tài khoản</p>
              <p className="text-xs text-zinc-400">Để thuê xe, bạn cần xác thực danh tính</p>
            </div>
          </div>
          <div className="flex gap-2 text-xs text-zinc-400">
            {['Thuê xe dễ dàng', 'Thanh toán nhanh', 'Ưu tiên xử lý'].map((b) => (
              <span key={b} className="flex items-center gap-1">
                <span className="size-1 rounded-full bg-[#22d3ee]" />
                {b}
              </span>
            ))}
          </div>
        </div>
        {/* CTA button */}
        <Link
          to={ROUTES.PROFILE_VERIFY}
          className="flex items-center justify-between bg-[#1a8a9d] px-5 py-3.5 transition-colors hover:bg-[#147485] active-press"
        >
          <span className="text-sm font-bold text-white">Bắt đầu xác thực ngay</span>
          <ChevronRight className="size-5 text-white/80" />
        </Link>
      </div>
    </div>
  )
}

function KycRejectedBanner({ reason }: { reason?: string }) {
  return (
    <div className="mx-4 rounded-2xl border border-red-200 bg-red-50 dark:border-red-800 dark:bg-red-950/30 p-4">
      <div className="flex items-start gap-3">
        <AlertTriangle className="size-5 text-red-600 dark:text-red-400 shrink-0 mt-0.5" />
        <div className="flex-1">
          <p className="text-sm font-semibold text-red-800 dark:text-red-300">Hồ sơ bị từ chối</p>
          {reason && <p className="mt-1 text-xs text-red-700 dark:text-red-400">{reason}</p>}
          <Link
            to={ROUTES.PROFILE_VERIFY}
            className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-red-700 dark:text-red-400 hover:underline"
          >
            Nộp lại hồ sơ <ChevronRight className="size-3" />
          </Link>
        </div>
      </div>
    </div>
  )
}

function InfoRow({ icon: Icon, label, value }: { icon: typeof User; label: string; value?: string }) {
  if (!value) return null
  return (
    <div className="flex items-start gap-3 py-2.5 border-b border-border last:border-0">
      <Icon className="size-4 text-muted-foreground shrink-0 mt-0.5" />
      <div className="min-w-0">
        <p className="text-[11px] text-muted-foreground">{label}</p>
        <p className="text-sm font-medium text-foreground truncate">{value}</p>
      </div>
    </div>
  )
}

// ─── Main Profile Page ────────────────────────────────────────────────────────
export default function ProfilePage() {
  const { user, logout } = useAuth()
  const { data: profile, isLoading: profileLoading } = useProfile()
  const { data: kyc, isLoading: kycLoading } = useKycStatus()

  if (profileLoading || kycLoading) return <ProfileSkeleton />

  const kycStatus = kyc?.status ?? 'UNVERIFIED'

  return (
    <div className="pb-24 space-y-4">
      {/* ── Profile Hero ── */}
      <div className="relative px-4 pt-6 pb-4">
        <div className="flex items-center gap-4">
          {/* Avatar */}
          <div className="relative">
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-[#1a8a9d] to-[#0c6b80] text-xl font-bold text-white shadow-md">
              {getInitials(user?.fullName ?? 'U')}
            </div>
            <div
              className={cn(
                'absolute -bottom-1 -right-1 h-4 w-4 rounded-full border-2 border-background',
                kycStatus === 'VERIFIED' ? 'bg-emerald-500' : 'bg-zinc-400'
              )}
            />
          </div>

          {/* Name & role */}
          <div className="min-w-0 flex-1">
            <h1 className="text-lg font-bold text-foreground truncate">
              {user?.fullName ?? 'Người dùng'}
            </h1>
            <p className="text-xs text-muted-foreground truncate">@{user?.username} · {user?.role}</p>
            <div className="mt-1.5">
              <VerificationBadge status={kycStatus} size="sm" />
            </div>
          </div>

          {/* Settings icon */}
          <button className="rounded-full p-2 hover:bg-accent transition-colors">
            <Settings className="size-5 text-muted-foreground" />
          </button>
        </div>
      </div>

      {/* ── Quick Dev/Demo State Switcher ── */}
      <KycDevTester currentStatus={kycStatus} />

      {/* ── KYC Status Banner ── */}
      {kycStatus === 'UNVERIFIED' && <KycUnverifiedCta />}
      {kycStatus === 'PENDING' && <KycPendingBanner />}
      {kycStatus === 'REJECTED' && <KycRejectedBanner reason={kyc?.rejectionReason} />}

      {/* ── Account Info Card ── */}
      <div className="mx-4 rounded-2xl border border-border bg-card p-4 space-y-0.5">
        <p className="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider mb-2">
          Thông tin tài khoản
        </p>
        <InfoRow icon={User} label="Họ và tên" value={user?.fullName} />
        <InfoRow icon={Mail} label="Email" value={user?.email} />
        <InfoRow icon={Shield} label="Vai trò" value={user?.role} />
      </div>

      {/* ── KYC Info Card (if verified or pending) ── */}
      {(kycStatus === 'VERIFIED' || kycStatus === 'PENDING') && kyc && (
        <div className="mx-4 rounded-2xl border border-border bg-card p-4">
          <p className="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider mb-2">
            Hồ sơ xác thực
          </p>
          <InfoRow icon={CreditCard} label="CCCD/CMND" value={kyc.cccdNumber} />
          <InfoRow
            icon={CalendarDays}
            label="Ngày sinh"
            value={kyc.dateOfBirth ? formatDate(kyc.dateOfBirth) : undefined}
          />
          <InfoRow icon={MapPin} label="Tỉnh/Thành" value={kyc.province} />
          {kyc.submittedAt && (
            <InfoRow
              icon={Clock}
              label="Ngày nộp"
              value={formatDate(kyc.submittedAt)}
            />
          )}
        </div>
      )}

      {/* ── Actions ── */}
      <div className="mx-4 rounded-2xl border border-border bg-card divide-y divide-border overflow-hidden">
        <button
          onClick={logout}
          className="flex w-full items-center gap-3 px-4 py-3.5 text-destructive hover:bg-destructive/5 transition-colors"
        >
          <LogOut className="size-4 shrink-0" />
          <span className="text-sm font-semibold">Đăng xuất</span>
        </button>
      </div>
    </div>
  )
}

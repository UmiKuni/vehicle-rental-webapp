import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, ArrowRight, Check, ShieldCheck } from 'lucide-react'
import { ROUTES } from '@/shared/constants'

export default function VerificationTermsPage() {
  const [accepted, setAccepted] = useState(false)
  const navigate = useNavigate()

  return (
    <main className="mx-auto w-full max-w-md space-y-5 px-4 pt-5 pb-28">
      <Link to={ROUTES.PROFILE} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Hồ sơ của tôi</Link>
      <div className="flex items-start gap-3">
        <div className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><ShieldCheck size={24} /></div>
        <div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Xác minh tài khoản</p><h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">Trước khi bắt đầu</h1><p className="mt-1 text-sm leading-6 text-muted-foreground">Xác minh danh tính và giấy phép lái xe để hoàn tất hồ sơ thuê xe.</p></div>
      </div>
      <section className="rounded-2xl border border-border bg-card p-4">
        <h2 className="text-sm font-bold text-foreground">Bạn cần chuẩn bị</h2>
        <ol className="mt-4 space-y-4">
          {[
            ['Giấy tờ tùy thân', 'CCCD hoặc hộ chiếu còn hiệu lực, chụp rõ thông tin.'],
            ['Giấy phép lái xe', 'Ảnh mặt trước và mặt sau, thông tin nhìn rõ.'],
            ['Thông tin liên hệ', 'Địa chỉ lưu trú và số điện thoại có thể liên lạc.'],
          ].map(([title, description], index) => <li key={title} className="flex gap-3"><span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-bold text-foreground">{index + 1}</span><div><p className="text-sm font-semibold text-foreground">{title}</p><p className="mt-0.5 text-xs leading-5 text-muted-foreground">{description}</p></div></li>)}
        </ol>
      </section>
      <div className="rounded-xl border border-[#1a8a9d]/20 bg-[#1a8a9d]/5 p-3 text-xs leading-5 text-muted-foreground">Thông tin và ảnh giấy tờ chỉ được dùng để xác minh tài khoản thuê xe. Hãy đảm bảo ảnh do bạn cung cấp và thông tin chính xác.</div>
      <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-border bg-card p-3.5">
        <input type="checkbox" checked={accepted} onChange={(event) => setAccepted(event.target.checked)} className="mt-0.5 size-4 accent-[#1a8a9d]" />
        <span className="text-xs leading-5 text-foreground">Tôi xác nhận đã đọc hướng dẫn và đồng ý cung cấp thông tin, giấy tờ để xác minh tài khoản.</span>
      </label>
      <button disabled={!accepted} onClick={() => navigate(ROUTES.VERIFY_IDENTITY)} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white transition hover:bg-[#147485] disabled:cursor-not-allowed disabled:opacity-45">Bắt đầu xác minh <ArrowRight size={17} /></button>
      <p className="flex items-center justify-center gap-1.5 text-[11px] text-muted-foreground"><Check size={13} className="text-[#1a8a9d]" />Bạn có thể lưu hồ sơ và hoàn tất sau</p>
    </main>
  )
}

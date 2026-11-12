import { Link } from 'react-router-dom'
import { ArrowLeft, Bell, BellRing } from 'lucide-react'
import { ROUTES } from '@/shared/constants'

export default function NotificationsPage() {
  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={ROUTES.HOME} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Trang chủ</Link>
      <div className="flex items-start justify-between"><div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Hộp thư</p><h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">Thông báo</h1><p className="mt-1 text-sm text-muted-foreground">Cập nhật về chuyến đi và tài khoản của bạn.</p></div><div className="flex size-11 items-center justify-center rounded-2xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><BellRing size={21} /></div></div>
      <section className="flex min-h-[42vh] flex-col items-center justify-center rounded-2xl border border-dashed border-border px-6 text-center">
        <div className="flex size-14 items-center justify-center rounded-2xl bg-muted text-muted-foreground"><Bell size={24} /></div>
        <h2 className="mt-4 text-base font-bold text-foreground">Bạn chưa có thông báo</h2>
        <p className="mt-1 max-w-xs text-sm leading-6 text-muted-foreground">Khi có cập nhật về đặt xe hoặc xác minh tài khoản, thông báo sẽ xuất hiện tại đây.</p>
      </section>
      <p className="text-center text-[11px] leading-5 text-muted-foreground">Hộp thư sẽ được đồng bộ khi API thông báo được kết nối.</p>
    </main>
  )
}

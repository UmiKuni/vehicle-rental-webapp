import { Link } from 'react-router-dom'
import { Car, Home } from 'lucide-react'
import { ROUTES } from '@/shared/constants'

export default function NotFoundPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-background p-4 text-center">
      <div className="flex h-20 w-20 items-center justify-center rounded-2xl bg-primary/10 mb-6">
        <Car className="size-10 text-primary" />
      </div>
      <h1 className="text-7xl font-black text-foreground">404</h1>
      <h2 className="mt-2 text-xl font-semibold text-foreground">Trang không tồn tại</h2>
      <p className="mt-3 max-w-sm text-sm text-muted-foreground">
        Trang bạn đang tìm kiếm không tồn tại hoặc đã bị di chuyển.
      </p>
      <Link
        to={ROUTES.DASHBOARD}
        className="mt-8 inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90"
      >
        <Home className="size-4" />
        Về trang chủ
      </Link>
    </div>
  )
}

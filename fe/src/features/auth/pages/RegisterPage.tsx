import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import { Eye, EyeOff, User, Lock, Mail, Phone, Car, AtSign, ShieldCheck, Zap, Sparkles } from 'lucide-react'
import { registerSchema, type RegisterFormValues } from '../schemas/register.schema'
import { useRegister } from '../hooks/useRegister'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { FormField } from '@/shared/components/ui/FormField'
import { ROUTES } from '@/shared/constants'

export default function RegisterPage() {
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)
  const { mutate: registerUser, isPending } = useRegister()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      fullName: '',
      username: '',
      email: '',
      phone: '',
      password: '',
      confirmPassword: '',
    },
  })

  const onSubmit = (values: RegisterFormValues) => registerUser(values)

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100 dark:bg-zinc-950 p-4 sm:p-6 lg:p-8">
      {/* Centered Card Container */}
      <div className="w-full max-w-md md:max-w-4xl overflow-hidden rounded-3xl border border-slate-200/80 dark:border-zinc-800 bg-white dark:bg-zinc-900 shadow-xl grid grid-cols-1 md:grid-cols-12">
        {/* Left Column (Brand & Benefits - Desktop only) */}
        <div className="hidden md:flex md:col-span-5 bg-[#0c1017] p-8 lg:p-10 flex-col justify-between text-white">
          {/* Top Logo */}
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-[#1a8a9d] to-[#0c6b80] text-white shadow-md">
              <Car className="size-5" />
            </div>
            <div>
              <span className="font-extrabold tracking-wider text-white text-sm">AUTOFLEET</span>
              <p className="text-[9px] font-mono tracking-widest text-[#22d3ee] -mt-0.5">SMART MOBILITY</p>
            </div>
          </div>

          {/* Middle Headline & Value Props */}
          <div className="my-6">
            <div className="inline-flex items-center gap-1.5 rounded-full border border-cyan-500/30 bg-cyan-500/10 px-3 py-1 text-[11px] font-semibold text-[#22d3ee] mb-3">
              <Sparkles className="size-3" />
              Đăng ký thành viên PWA
            </div>
            <h1 className="text-2xl lg:text-3xl font-extrabold tracking-tight text-white leading-tight">
              Bắt đầu hành trình
              <span className="block text-[#22d3ee] mt-1">Thuê xe tự lái 4.0</span>
            </h1>
            <p className="mt-3 text-xs text-zinc-400 leading-relaxed max-w-xs">
              Tạo tài khoản chỉ trong 30 giây để nhận ưu đãi thuê xe đầu tiên và xác thực GPLX nhanh chóng.
            </p>

            <div className="mt-6 space-y-3">
              {[
                { icon: Zap, text: 'Đặt xe siêu tốc, nhận xe không thủ tục giấy tờ' },
                { icon: ShieldCheck, text: 'Xác thực danh tính KYC trực tuyến trong 2 phút' },
                { icon: Car, text: 'Hơn 60+ dòng xe tự lái đời mới được bảo dưỡng định kỳ' },
              ].map(({ icon: Icon, text }) => (
                <div key={text} className="flex items-start gap-2.5 text-xs text-zinc-300">
                  <div className="flex h-5 w-5 shrink-0 items-center justify-center rounded-md bg-white/10 text-[#22d3ee] mt-0.5">
                    <Icon className="size-3" />
                  </div>
                  <span className="leading-snug">{text}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Bottom Social Proof Widget */}
          <div className="rounded-2xl border border-white/10 bg-white/5 p-3.5 flex items-center gap-3">
            <span className="text-xl">⭐️</span>
            <div className="min-w-0">
              <p className="text-xs font-bold text-white truncate">Đánh giá 4.9/5 sao</p>
              <p className="text-[11px] text-zinc-400 truncate">Từ hơn 15,000+ chuyến đi an toàn</p>
            </div>
          </div>
        </div>

        {/* Right Form Column (App Native Form) */}
        <div className="md:col-span-7 p-6 sm:p-8 lg:p-10 flex flex-col justify-center bg-white dark:bg-zinc-900">
          {/* Mobile-only App Icon Header */}
          <div className="md:hidden flex flex-col items-center text-center pb-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-[#1a8a9d] to-[#0c6b80] text-white shadow-md shadow-[#1a8a9d]/20 mb-2">
              <Car className="size-6" />
            </div>
            <span className="text-base font-extrabold tracking-tight text-foreground">AutoFleet PWA</span>
            <p className="text-[11px] text-muted-foreground">Thuê xe tự lái thông minh & nhanh chóng</p>
          </div>

          {/* Segmented Control Switcher (Login / Register) */}
          <div className="grid grid-cols-2 p-1 bg-slate-100 dark:bg-zinc-800 rounded-2xl mb-5">
            <Link
              to={ROUTES.LOGIN}
              className="py-2 text-center text-xs font-semibold text-muted-foreground hover:text-foreground transition-all flex items-center justify-center"
            >
              Đăng nhập
            </Link>
            <div className="py-2 text-center text-xs font-bold rounded-xl bg-white dark:bg-zinc-900 text-foreground shadow-xs">
              Đăng ký
            </div>
          </div>

          {/* Greeting */}
          <div className="mb-4">
            <h2 className="text-xl font-bold tracking-tight text-foreground flex items-center gap-2">
              Tạo tài khoản mới ✨
            </h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Đăng ký thành viên để trải nghiệm nhận xe không chạm
            </p>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-3" noValidate>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <FormField label="Họ và tên" error={errors.fullName?.message}>
                <div className="relative">
                  <User className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    placeholder="Nguyễn Văn A"
                    autoFocus
                    className="h-10 rounded-xl bg-slate-50 dark:bg-zinc-800/60 pl-10 text-xs focus-visible:border-[#1a8a9d]"
                    {...register('fullName')}
                  />
                </div>
              </FormField>

              <FormField label="Tên đăng nhập" error={errors.username?.message}>
                <div className="relative">
                  <AtSign className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    placeholder="nguyenvana"
                    autoComplete="username"
                    className="h-10 rounded-xl bg-slate-50 dark:bg-zinc-800/60 pl-10 text-xs focus-visible:border-[#1a8a9d]"
                    {...register('username')}
                  />
                </div>
              </FormField>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <FormField label="Email" error={errors.email?.message}>
                <div className="relative">
                  <Mail className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    type="email"
                    placeholder="a@gmail.com"
                    autoComplete="email"
                    className="h-10 rounded-xl bg-slate-50 dark:bg-zinc-800/60 pl-10 text-xs focus-visible:border-[#1a8a9d]"
                    {...register('email')}
                  />
                </div>
              </FormField>

              <FormField label="Số điện thoại" error={errors.phone?.message}>
                <div className="relative">
                  <Phone className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    type="tel"
                    placeholder="0912345678"
                    autoComplete="tel"
                    className="h-10 rounded-xl bg-slate-50 dark:bg-zinc-800/60 pl-10 text-xs focus-visible:border-[#1a8a9d]"
                    {...register('phone')}
                  />
                </div>
              </FormField>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <FormField label="Mật khẩu" error={errors.password?.message}>
                <div className="relative">
                  <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    type={showPassword ? 'text' : 'password'}
                    placeholder="••••••••"
                    autoComplete="new-password"
                    className="h-10 rounded-xl bg-slate-50 dark:bg-zinc-800/60 pl-10 pr-9 text-xs focus-visible:border-[#1a8a9d]"
                    {...register('password')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((v) => !v)}
                    className="absolute right-2.5 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-600 p-1"
                    aria-label={showPassword ? 'Ẩn' : 'Hiện'}
                  >
                    {showPassword ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
                  </button>
                </div>
              </FormField>

              <FormField label="Xác nhận mật khẩu" error={errors.confirmPassword?.message}>
                <div className="relative">
                  <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    type={showConfirmPassword ? 'text' : 'password'}
                    placeholder="••••••••"
                    autoComplete="new-password"
                    className="h-10 rounded-xl bg-slate-50 dark:bg-zinc-800/60 pl-10 pr-9 text-xs focus-visible:border-[#1a8a9d]"
                    {...register('confirmPassword')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirmPassword((v) => !v)}
                    className="absolute right-2.5 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-600 p-1"
                    aria-label={showConfirmPassword ? 'Ẩn' : 'Hiện'}
                  >
                    {showConfirmPassword ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
                  </button>
                </div>
              </FormField>
            </div>

            <Button
              type="submit"
              loading={isPending}
              className="mt-4 w-full h-11 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-semibold text-sm shadow-sm transition-colors active-press"
            >
              Tạo tài khoản & Đăng nhập
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}

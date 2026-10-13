import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Eye, EyeOff, User, Lock, Headphones, Car, KeyRound } from 'lucide-react'
import { loginSchema, type LoginFormValues } from '../schemas/login.schema'
import { useLogin } from '../hooks/useLogin'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { FormField } from '@/shared/components/ui/FormField'

export default function LoginPage() {
  const [showPassword, setShowPassword] = useState(false)
  const { mutate: login, isPending } = useLogin()

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { username: '', password: '' },
  })

  const fillQuickAccount = (user: string, pass: string) => {
    setValue('username', user, { shouldValidate: true })
    setValue('password', pass, { shouldValidate: true })
  }

  const onSubmit = (values: LoginFormValues) => login(values)

  return (
    <div className="flex min-h-screen items-center justify-center bg-[#ebf1f6] dark:bg-zinc-950 p-4 sm:p-6 lg:p-8">
      {/* Centered Modal / Card Container */}
      <div className="w-full max-w-4xl overflow-hidden rounded-3xl border border-slate-200/80 dark:border-zinc-800 bg-white dark:bg-zinc-900 shadow-xl grid grid-cols-1 md:grid-cols-12">
        {/* Left Dark Column (Brand & Greeting) */}
        <div className="md:col-span-5 bg-[#0c1017] p-8 sm:p-10 flex flex-col justify-between text-white min-h-[380px] md:min-h-[480px]">
          {/* Top Logo */}
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/10 text-cyan-400">
              <Car className="size-4" />
            </div>
            <div>
              <span className="font-extrabold tracking-wider text-white text-sm">AUTOFLEET</span>
              <p className="text-[9px] font-mono tracking-widest text-zinc-400 -mt-0.5">C O M P A N Y</p>
            </div>
          </div>

          {/* Middle Headline */}
          <div className="my-8 md:my-0">
            <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight text-white leading-tight">
              Chào mừng
              <span className="block text-[#22d3ee] mt-1">Hệ thống quản trị</span>
            </h1>
            <p className="mt-3.5 text-xs text-zinc-400 leading-relaxed max-w-xs">
              Nền tảng quản trị đội xe toàn diện, giúp bạn quản lý vận hành, tài chính, kiểm soát phương tiện và nhân sự một cách hiệu quả.
            </p>
          </div>

          {/* Bottom Support Widget */}
          <div className="rounded-2xl border border-white/10 bg-white/5 p-3.5 flex items-center gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white/10 text-[#22d3ee]">
              <Headphones className="size-4" />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-semibold text-white truncate">Cần hỗ trợ triển khai?</p>
              <p className="text-[11px] text-zinc-400 truncate">Liên hệ bộ phận kỹ thuật</p>
            </div>
          </div>
        </div>

        {/* Right Form Column */}
        <div className="md:col-span-7 p-8 sm:p-10 lg:p-12 flex flex-col justify-center bg-white dark:bg-zinc-900">
          <div className="mb-6">
            <h2 className="text-2xl font-bold tracking-tight text-zinc-900 dark:text-zinc-100">
              Đăng nhập vào hệ thống
            </h2>
            <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">
              Vui lòng sử dụng tài khoản được cấp để đăng nhập.
            </p>
          </div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
            {/* Username / Email field */}
            <FormField label="Tên người dùng / Email" error={errors.username?.message}>
              <div className="relative">
                <User className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                <Input
                  id="username"
                  placeholder="admin hoặc email@domain.com"
                  autoComplete="username"
                  autoFocus
                  className="h-11 rounded-xl border border-slate-200 dark:border-zinc-800 bg-[#f0f4f8] dark:bg-zinc-800/70 pl-10 text-xs font-medium text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d] focus-visible:ring-offset-0 focus:bg-white dark:focus:bg-zinc-800 transition-all"
                  {...register('username')}
                />
              </div>
            </FormField>

            {/* Password field */}
            <FormField
              label="Mật khẩu"
              rightAction={
                <a
                  href="#forgot"
                  onClick={(e) => e.preventDefault()}
                  className="text-xs font-medium text-zinc-500 dark:text-zinc-400 hover:text-zinc-800 dark:hover:text-zinc-200 transition-colors"
                >
                  Quên mật khẩu?
                </a>
              }
              error={errors.password?.message}
            >
              <div className="relative">
                <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                <Input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  placeholder="••••••••"
                  autoComplete="current-password"
                  className="h-11 rounded-xl border border-slate-200 dark:border-zinc-800 bg-[#f0f4f8] dark:bg-zinc-800/70 pl-10 pr-10 text-xs font-medium text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus-visible:border-[#1a8a9d] focus-visible:ring-1 focus-visible:ring-[#1a8a9d] focus-visible:ring-offset-0 focus:bg-white dark:focus:bg-zinc-800 transition-all"
                  {...register('password')}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-3.5 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-200 transition-colors p-1"
                  aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                >
                  {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
            </FormField>

            {/* Submit Button */}
            <Button
              type="submit"
              loading={isPending}
              className="mt-6 w-full h-11 rounded-lg bg-[#1a8a9d] hover:bg-[#147485] text-white font-semibold text-sm shadow-sm transition-colors active-press"
            >
              Đăng nhập
            </Button>
          </form>

          {/* Quick Demo Test Fill */}
          <div className="mt-6 pt-5 border-t border-slate-100 dark:border-zinc-800/80">
            <div className="flex items-center gap-1.5 text-xs text-zinc-500 mb-2 font-medium">
              <KeyRound className="size-3.5 text-zinc-400" />
              <span>Tài khoản thử nghiệm nhanh (Click để điền):</span>
            </div>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => fillQuickAccount('admin', 'admin123')}
                className="inline-flex items-center gap-1.5 rounded-md border border-slate-200 dark:border-zinc-800 bg-[#f8fafc] dark:bg-zinc-800/50 px-2.5 py-1 text-xs font-medium text-zinc-700 dark:text-zinc-300 hover:bg-slate-100 dark:hover:bg-zinc-800 transition-colors"
              >
                <span className="size-1.5 rounded-full bg-blue-500" />
                Admin
              </button>
              <button
                type="button"
                onClick={() => fillQuickAccount('manager', 'manager123')}
                className="inline-flex items-center gap-1.5 rounded-md border border-slate-200 dark:border-zinc-800 bg-[#f8fafc] dark:bg-zinc-800/50 px-2.5 py-1 text-xs font-medium text-zinc-700 dark:text-zinc-300 hover:bg-slate-100 dark:hover:bg-zinc-800 transition-colors"
              >
                <span className="size-1.5 rounded-full bg-emerald-500" />
                Manager
              </button>
              <button
                type="button"
                onClick={() => fillQuickAccount('staff', 'staff123')}
                className="inline-flex items-center gap-1.5 rounded-md border border-slate-200 dark:border-zinc-800 bg-[#f8fafc] dark:bg-zinc-800/50 px-2.5 py-1 text-xs font-medium text-zinc-700 dark:text-zinc-300 hover:bg-slate-100 dark:hover:bg-zinc-800 transition-colors"
              >
                <span className="size-1.5 rounded-full bg-zinc-400" />
                Staff
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import { Eye, EyeOff, User, Lock, Car, KeyRound, Sparkles, ShieldCheck, Zap } from 'lucide-react'
import { loginSchema, type LoginFormValues } from '../schemas/login.schema'
import { useLogin } from '../hooks/useLogin'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { FormField } from '@/shared/components/ui/FormField'
import { ROUTES } from '@/shared/constants'

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
              Smart Car Rental Platform
            </div>
            <h1 className="text-2xl lg:text-3xl font-extrabold tracking-tight text-white leading-tight">
              Discover Your Journey
              <span className="block text-[#22d3ee] mt-1">Next-Gen Self-Drive Rentals</span>
            </h1>
            <p className="mt-3 text-xs text-zinc-400 leading-relaxed max-w-xs">
              Experience contactless booking, 100% digital PWA paperwork, and instant pickup within 1 minute.
            </p>

            <div className="mt-6 space-y-3">
              {[
                { icon: Zap, text: 'Ultra-fast booking, paperless vehicle pickup' },
                { icon: ShieldCheck, text: 'Online KYC identity verification in 2 minutes' },
                { icon: Car, text: '60+ late-model self-drive vehicles, routinely serviced' },
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
              <p className="text-xs font-bold text-white truncate">Rated 4.9/5 Stars</p>
              <p className="text-[11px] text-zinc-400 truncate">From over 15,000+ safe journeys</p>
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
            <p className="text-[11px] text-muted-foreground">Smart & fast self-drive car rental</p>
          </div>

          {/* Segmented Control Switcher (Login / Register) */}
          <div className="grid grid-cols-2 p-1 bg-slate-100 dark:bg-zinc-800 rounded-2xl mb-5">
            <div className="py-2 text-center text-xs font-bold rounded-xl bg-white dark:bg-zinc-900 text-foreground shadow-xs">
              Sign In
            </div>
            <Link
              to={ROUTES.REGISTER}
              className="py-2 text-center text-xs font-semibold text-muted-foreground hover:text-foreground transition-all flex items-center justify-center"
            >
              Register
            </Link>
          </div>

          {/* Greeting */}
          <div className="mb-5">
            <h2 className="text-xl font-bold tracking-tight text-foreground flex items-center gap-2">
              Welcome back! 👋
            </h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Sign in to rent vehicles or manage your journeys
            </p>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-3.5" noValidate>
            <FormField label="Username or Email" error={errors.username?.message}>
              <div className="relative">
                <User className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                <Input
                  id="username"
                  placeholder="user, admin or email..."
                  autoComplete="username"
                  autoFocus
                  className="h-11 rounded-xl bg-slate-50 dark:bg-zinc-800/60 pl-10 text-xs font-medium focus-visible:border-[#1a8a9d]"
                  {...register('username')}
                />
              </div>
            </FormField>

            <FormField
              label="Password"
              rightAction={
                <a
                  href="#forgot"
                  onClick={(e) => e.preventDefault()}
                  className="text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
                >
                  Forgot password?
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
                  className="h-11 rounded-xl bg-slate-50 dark:bg-zinc-800/60 pl-10 pr-10 text-xs font-medium focus-visible:border-[#1a8a9d]"
                  {...register('password')}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-3.5 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-200 p-1"
                  aria-label={showPassword ? 'Hide' : 'Show'}
                >
                  {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
            </FormField>

            <Button
              type="submit"
              loading={isPending}
              className="mt-5 w-full h-11 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-semibold text-sm shadow-sm transition-colors active-press"
            >
              Sign In Now
            </Button>
          </form>

          {/* Quick Demo Test Fill */}
          <div className="mt-5 pt-4 border-t border-border/60">
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground mb-2 font-medium">
              <KeyRound className="size-3.5 text-zinc-400" />
              <span>Demo test accounts (Click to auto-fill):</span>
            </div>
            <div className="flex flex-wrap gap-1.5">
              {[
                { label: 'User (KYC)', u: 'user', p: 'user123', bg: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-300/40' },
                { label: 'Admin',      u: 'admin', p: 'admin123', bg: 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-300/40' },
                { label: 'Manager',    u: 'manager', p: 'manager123', bg: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-300/40' },
                { label: 'Staff',      u: 'staff', p: 'staff123', bg: 'bg-zinc-500/10 text-zinc-600 dark:text-zinc-400 border-zinc-300/40' },
              ].map(({ label, u, p, bg }) => (
                <button
                  key={u}
                  type="button"
                  onClick={() => fillQuickAccount(u, p)}
                  className={`inline-flex items-center gap-1 rounded-lg border px-2.5 py-1 text-xs font-semibold hover:opacity-80 transition-all ${bg}`}
                >
                  <span className="size-1.5 rounded-full bg-current" />
                  {label}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

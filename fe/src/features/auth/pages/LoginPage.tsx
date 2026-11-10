import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Eye, EyeOff, User, Lock, Car, AlertCircle } from 'lucide-react'
import { loginSchema, type LoginFormValues } from '../schemas/login.schema'
import { useLogin } from '../hooks/useLogin'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { FormField } from '@/shared/components/ui/FormField'
import { APP_NAME, ROUTES } from '@/shared/constants'
import { getErrorMessage, getFieldErrors } from '@/shared/lib/query-client'
import { useAuth } from '@/shared/contexts/AuthContext'

export default function LoginPage() {
  const [showPassword, setShowPassword] = useState(false)
  const [portalChoice, setPortalChoice] = useState<'admin' | 'customer' | null>(null)
  const location = useLocation()
  const navigate = useNavigate()
  const { logout } = useAuth()
  const isAdminLogin = location.pathname === ROUTES.ADMIN_LOGIN
  const isStandaloneLogin = location.pathname === ROUTES.LOGIN
  const { mutate: login, isPending, error } = useLogin(setPortalChoice)

  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { username: '', password: '' },
  })

  useEffect(() => {
    if (!error) return
    Object.entries(getFieldErrors(error)).forEach(([field, messages]) => {
      const name = field.toLowerCase()
      if ((name === 'username' || name === 'password') && messages[0]) {
        setError(name, { type: 'server', message: messages[0] })
      }
    })
  }, [error, setError])

  const onSubmit = (values: LoginFormValues) => login(values)

  return (
    <div className={`flex ${(isAdminLogin || isStandaloneLogin) ? 'min-h-dvh' : 'min-h-[calc(100dvh-6rem)]'} items-center justify-center bg-slate-100 p-4 dark:bg-zinc-950 sm:p-6 lg:p-8`}>
      {/* Centered Card Container */}
      <div className="grid w-full max-w-md grid-cols-1 overflow-hidden rounded-2xl border border-[#d9e0df] bg-white shadow-[0_18px_55px_rgba(18,39,38,.12)] dark:border-zinc-800 dark:bg-zinc-900 md:max-w-5xl md:grid-cols-12">
        {/* Left Column (Brand & Benefits - Desktop only) */}
        <div className="hidden flex-col justify-between bg-[#142222] p-9 text-white md:col-span-5 md:flex lg:p-12">
          {/* Top Logo */}
          <div className="flex items-center gap-3">
            <div className="grid size-9 place-items-center rounded-md bg-[#287f7e] text-white"><Car className="size-[18px]" /></div>
            <div>
              <span className="text-sm font-bold tracking-[-.02em] text-white">{APP_NAME}</span>
              <p className="mb-0 mt-0.5 text-[10px] font-medium tracking-[.08em] text-[#9bb5b3]">{isAdminLogin ? 'ADMIN CONSOLE' : 'CUSTOMER PORTAL'}</p>
            </div>
          </div>

          <div className="my-12 max-w-sm">
            <p className="mb-4 text-[11px] font-bold uppercase tracking-[.14em] text-[#8fb8b4]">{isAdminLogin ? 'PLATFORM OPERATIONS' : 'VEHICLE RENTALS'}</p>
            <h1 className="text-[32px] font-bold leading-[1.12] tracking-[-.045em] text-white lg:text-[38px]">
              {isAdminLogin ? 'Rental operations, in one place.' : 'A simpler way to rent a vehicle.'}
            </h1>
            <p className="mb-0 mt-4 max-w-[34ch] text-sm font-medium leading-6 text-[#bdc9c7]">
              {isAdminLogin ? 'Review fleet listings, maintain the vehicle catalog and manage account access.' : 'Find a vehicle, arrange your booking and keep your trip details together.'}
            </p>
          </div>

          <p className="mb-0 border-t border-white/15 pt-4 text-[11px] font-medium text-[#9bb0ae]">{isAdminLogin ? 'Authorized staff only' : 'Your account · bookings · vehicle details'}</p>
        </div>

        {/* Right Form Column (App Native Form) */}
        <div className="flex flex-col justify-center bg-white p-6 dark:bg-zinc-900 sm:p-9 lg:p-12 md:col-span-7">
          {/* Mobile-only App Icon Header */}
          <div className="mb-5 flex items-center gap-2.5 md:hidden">
            <div className="grid size-8 place-items-center rounded-md bg-[#287f7e] text-white"><Car className="size-4" /></div>
            <span className="text-sm font-bold tracking-tight text-foreground">{isAdminLogin ? `${APP_NAME} Admin` : APP_NAME}</span>
          </div>

          {/* Segmented Control Switcher (Login / Register) */}
          {isAdminLogin ? (
            <div className="mb-6 grid grid-cols-2 border-b border-slate-200 dark:border-zinc-700">
              <span aria-current="page" className="flex min-h-11 items-center justify-center border-b-2 border-[#287f7e] px-3 text-[13px] font-bold text-[#173b3d] dark:text-white">
                Admin
              </span>
              <Link to={ROUTES.LOGIN} className="flex min-h-11 items-center justify-center px-3 text-[13px] font-semibold text-slate-600 transition hover:text-[#173b3d] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#287f7e] dark:text-zinc-300 dark:hover:text-white">
                Customer sign in
              </Link>
            </div>
          ) : (
            <div className="mb-6 grid grid-cols-2 border-b border-slate-200 dark:border-zinc-700">
              <span aria-current="page" className="flex min-h-11 items-center justify-center border-b-2 border-[#287f7e] text-[13px] font-bold text-[#173b3d] dark:text-white">Sign in</span>
              <Link to={isStandaloneLogin ? ROUTES.REGISTER : `${ROUTES.PROFILE_AUTH}?mode=register`} state={location.state} className="flex min-h-11 items-center justify-center text-[13px] font-semibold text-slate-600 transition hover:text-[#173b3d] dark:text-zinc-300 dark:hover:text-white">Sign Up</Link>
            </div>
          )}

          {/* Greeting */}
          <div className="mb-5">
            <h2 className="flex items-center gap-2 text-xl font-bold tracking-tight text-foreground">{isAdminLogin ? 'Admin sign in' : 'Sign in'}</h2>
            <p className="mt-1 text-sm font-medium leading-5 text-muted-foreground">
              {isAdminLogin ? 'Use an administrator account to continue to the control panel.' : 'Sign in to continue booking and manage your trips.'}
            </p>
          </div>

          {!isAdminLogin && (
            <div className="-mt-2 mb-4 text-[13px] text-muted-foreground">
              Platform administrator? <Link to={ROUTES.ADMIN_LOGIN} className="font-bold text-[#176f70] underline-offset-4 hover:underline">Sign in to admin</Link>
            </div>
          )}

          {/* Form */}
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-3.5" noValidate>
            <FormField label="Email / Phone number" error={errors.username?.message}>
              <div className="relative">
                <User className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                <Input
                  id="username"
                  placeholder="Email or phone number"
                  autoComplete="username"
                  autoFocus
                  className="h-12 rounded-lg bg-slate-50 dark:bg-zinc-800/60 pl-10 text-sm font-semibold focus-visible:border-[#287f7e]"
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
                  className="text-xs font-semibold text-muted-foreground hover:text-foreground transition-colors"
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
                  className="h-12 rounded-lg bg-slate-50 dark:bg-zinc-800/60 pl-10 pr-10 text-sm font-semibold focus-visible:border-[#287f7e]"
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

            {error && <p className="auth-form-error" role="alert"><AlertCircle size={16} />{getErrorMessage(error)}</p>}
            <Button
              type="submit"
              loading={isPending}
              className="mt-5 h-12 w-full rounded-lg bg-[#176f70] text-sm font-semibold text-white shadow-none transition-colors hover:bg-[#125d5e] active-press"
            >
              Sign In
            </Button>
          </form>
        </div>
      </div>

      {portalChoice && (
        <div className="fixed inset-0 z-[100] grid place-items-center bg-[#101719a8] p-4" role="presentation">
          <section role="dialog" aria-modal="true" aria-labelledby="portal-choice-title" className="w-full max-w-md rounded-xl border border-slate-200 bg-white p-6 shadow-[0_16px_48px_rgba(0,0,0,.18)] dark:border-zinc-700 dark:bg-zinc-900">
            <p className="mb-2 text-xs font-semibold text-[#176f70]">Account type</p>
            <h2 id="portal-choice-title" className="m-0 text-lg font-semibold tracking-tight text-slate-900 dark:text-white">
              {portalChoice === 'customer' ? 'Switch to the customer portal?' : 'Open the admin workspace?'}
            </h2>
            <p className="mb-0 mt-2 text-sm leading-6 text-slate-600 dark:text-zinc-300">
              {portalChoice === 'customer'
                ? 'This is a customer account. The mobile portal is designed for browsing vehicles, bookings and your profile.'
                : 'This is an administrator account. The admin workspace is designed for fleet and platform management.'}
            </p>
            <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              {portalChoice === 'customer' ? (
                <button type="button" onClick={() => { logout(); setPortalChoice(null); reset() }} className="h-10 rounded-lg border border-slate-200 px-4 text-xs font-bold text-slate-600 transition hover:bg-slate-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800">Use a different account</button>
              ) : (
                <button type="button" onClick={() => { logout(); setPortalChoice(null); reset() }} className="h-10 rounded-lg border border-slate-200 px-4 text-xs font-bold text-slate-600 transition hover:bg-slate-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800">Use a different account</button>
              )}
              <button type="button" onClick={() => { const destination = portalChoice === 'admin' ? ROUTES.DASHBOARD : ROUTES.SEARCH; setPortalChoice(null); navigate(destination, { replace: true }) }} className="h-10 rounded-lg bg-[#16868a] px-4 text-xs font-bold text-white transition hover:bg-[#116d72]">{portalChoice === 'customer' ? 'Continue to mobile portal' : 'Continue to admin workspace'}</button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}

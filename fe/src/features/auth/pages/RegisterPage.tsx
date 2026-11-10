import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useLocation } from 'react-router-dom'
import { Eye, EyeOff, User, Lock, Mail, Phone, Car, AtSign, AlertCircle } from 'lucide-react'
import { registerSchema, type RegisterFormValues } from '../schemas/register.schema'
import { useRegister } from '../hooks/useRegister'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { FormField } from '@/shared/components/ui/FormField'
import { APP_NAME, ROUTES } from '@/shared/constants'
import { getErrorMessage, getFieldErrors } from '@/shared/lib/query-client'

export default function RegisterPage() {
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)
  const location = useLocation()
  const isStandaloneRegister = location.pathname === ROUTES.REGISTER
  const { mutate: registerUser, isPending, error } = useRegister()

  const {
    register,
    handleSubmit,
    setError,
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

  useEffect(() => {
    if (!error) return
    const validFields = ['fullName', 'username', 'email', 'phone', 'password', 'confirmPassword']
    Object.entries(getFieldErrors(error)).forEach(([field, messages]) => {
      const name = field.charAt(0).toLowerCase() + field.slice(1)
      if (validFields.includes(name) && messages[0]) {
        setError(name as keyof RegisterFormValues, { type: 'server', message: messages[0] })
      }
    })
  }, [error, setError])

  const onSubmit = (values: RegisterFormValues) => registerUser(values)

  return (
    <div className={`flex ${isStandaloneRegister ? 'min-h-dvh' : 'min-h-[calc(100dvh-6rem)]'} items-center justify-center bg-slate-100 p-4 dark:bg-zinc-950 sm:p-6 lg:p-8`}>
      {/* Centered Card Container */}
      <div className="grid w-full max-w-md grid-cols-1 overflow-hidden rounded-2xl border border-[#d9e0df] bg-white shadow-[0_18px_55px_rgba(18,39,38,.12)] dark:border-zinc-800 dark:bg-zinc-900 md:max-w-5xl md:grid-cols-12">
        {/* Left Column (Brand & Benefits - Desktop only) */}
        <div className="hidden flex-col justify-between bg-[#142222] p-9 text-white md:col-span-5 md:flex lg:p-12">
          {/* Top Logo */}
          <div className="flex items-center gap-3">
            <div className="grid size-9 place-items-center rounded-md bg-[#287f7e] text-white"><Car className="size-[18px]" /></div>
            <div>
              <span className="text-sm font-bold tracking-[-.02em] text-white">{APP_NAME}</span>
              <p className="mb-0 mt-0.5 text-[10px] font-medium tracking-[.08em] text-[#9bb5b3]">CUSTOMER PORTAL</p>
            </div>
          </div>

          {/* Middle Headline & Value Props */}
          <div className="my-12 max-w-sm">
            <p className="mb-4 text-[11px] font-bold uppercase tracking-[.14em] text-[#8fb8b4]">ACCOUNT SETUP</p>
            <h1 className="text-[32px] font-bold leading-[1.12] tracking-[-.045em] text-white lg:text-[38px]">
              Create your rental account.
            </h1>
            <p className="mb-0 mt-4 max-w-[34ch] text-sm font-medium leading-6 text-[#bdc9c7]">
              Save your details, complete driver verification and manage your bookings in one place.
            </p>
          </div>

          <p className="mb-0 border-t border-white/15 pt-4 text-[11px] font-medium text-[#9bb0ae]">CUSTOMER PORTAL · ACCOUNT SETUP</p>
        </div>

        {/* Right Form Column (App Native Form) */}
          <div className="flex flex-col justify-center bg-white p-6 dark:bg-zinc-900 sm:p-9 lg:p-12 md:col-span-7">
          {/* Mobile-only App Icon Header */}
            <div className="mb-5 flex items-center gap-2.5 md:hidden">
              <div className="grid size-8 place-items-center rounded-md bg-[#287f7e] text-white"><Car className="size-4" /></div>
              <span className="text-sm font-bold tracking-tight text-foreground">{APP_NAME}</span>
          </div>

          {/* Segmented Control Switcher (Login / Register) */}
          <div className="mb-6 grid grid-cols-2 border-b border-slate-200 dark:border-zinc-700">
            <Link
              to={isStandaloneRegister ? ROUTES.LOGIN : `${ROUTES.PROFILE_AUTH}?mode=login`}
              state={location.state}
              className="flex min-h-11 items-center justify-center text-[13px] font-semibold text-slate-600 transition hover:text-[#173b3d] dark:text-zinc-300 dark:hover:text-white"
            >
              Sign In
            </Link>
            <div aria-current="page" className="flex min-h-11 items-center justify-center border-b-2 border-[#287f7e] text-[13px] font-bold text-[#173b3d] dark:text-white">
              Sign Up
            </div>
          </div>

          {/* Greeting */}
          <div className="mb-4">
            <h2 className="text-xl font-bold tracking-tight text-foreground flex items-center gap-2">Create an account</h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Enter your details below to create your account.
            </p>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-3" noValidate>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <FormField label="Full name" error={errors.fullName?.message}>
                <div className="relative">
                  <User className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    placeholder="John Doe"
                    autoFocus
                    className="h-11 rounded-lg bg-slate-50 text-sm font-semibold dark:bg-zinc-800/60 pl-10 focus-visible:border-[#287f7e]"
                    {...register('fullName')}
                  />
                </div>
              </FormField>

              <FormField label="Username" error={errors.username?.message}>
                <div className="relative">
                  <AtSign className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    placeholder="johndoe"
                    autoComplete="username"
                    className="h-11 rounded-lg bg-slate-50 text-sm font-semibold dark:bg-zinc-800/60 pl-10 focus-visible:border-[#287f7e]"
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
                    className="h-11 rounded-lg bg-slate-50 text-sm font-semibold dark:bg-zinc-800/60 pl-10 focus-visible:border-[#287f7e]"
                    {...register('email')}
                  />
                </div>
              </FormField>

              <FormField label="Phone number" error={errors.phone?.message}>
                <div className="relative">
                  <Phone className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    type="tel"
                    placeholder="0912345678"
                    autoComplete="tel"
                    className="h-11 rounded-lg bg-slate-50 text-sm font-semibold dark:bg-zinc-800/60 pl-10 focus-visible:border-[#287f7e]"
                    {...register('phone')}
                  />
                </div>
              </FormField>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <FormField label="Password" error={errors.password?.message}>
                <div className="relative">
                  <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    type={showPassword ? 'text' : 'password'}
                    placeholder="••••••••"
                    autoComplete="new-password"
                    className="h-11 rounded-lg bg-slate-50 text-sm font-semibold dark:bg-zinc-800/60 pl-10 pr-9 focus-visible:border-[#287f7e]"
                    {...register('password')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((v) => !v)}
                    className="absolute right-2.5 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-600 p-1"
                    aria-label={showPassword ? 'Hide' : 'Show'}
                  >
                    {showPassword ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
                  </button>
                </div>
              </FormField>

              <FormField label="Confirm password" error={errors.confirmPassword?.message}>
                <div className="relative">
                  <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-zinc-400 pointer-events-none" />
                  <Input
                    type={showConfirmPassword ? 'text' : 'password'}
                    placeholder="••••••••"
                    autoComplete="new-password"
                    className="h-11 rounded-lg bg-slate-50 text-sm font-semibold dark:bg-zinc-800/60 pl-10 pr-9 focus-visible:border-[#287f7e]"
                    {...register('confirmPassword')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirmPassword((v) => !v)}
                    className="absolute right-2.5 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-600 p-1"
                    aria-label={showConfirmPassword ? 'Hide' : 'Show'}
                  >
                    {showConfirmPassword ? <EyeOff className="size-3.5" /> : <Eye className="size-3.5" />}
                  </button>
                </div>
              </FormField>
            </div>

            {error && <p className="auth-form-error" role="alert"><AlertCircle size={16} />{getErrorMessage(error)}</p>}
            <Button
              type="submit"
              loading={isPending}
              className="mt-4 h-12 w-full rounded-lg bg-[#176f70] text-sm font-semibold text-white shadow-none transition-colors hover:bg-[#125d5e] active-press"
            >
              Create Account
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}

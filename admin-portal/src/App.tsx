import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Navigate, NavLink, Route, Routes } from 'react-router-dom'
import { CarFront, Layers3, LogOut, ShieldCheck, Users, ArrowRight } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { api, session, signIn, signOut } from './api'
import type { Identity } from './api'
import { Failure, Loading } from './ui'
import { Vehicles } from './Vehicles'
import { Catalog } from './Catalog'
import { UserAccess } from './UserAccess'

const loginSchema = z.object({ identifier: z.string().trim().min(1, 'Enter your email or phone.'), password: z.string().min(1, 'Enter your password.') })

function Login({ onSuccess }: { onSuccess: () => void }) {
  const [error, setError] = useState<unknown>(null)
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<z.infer<typeof loginSchema>>({ resolver: zodResolver(loginSchema) })
  return <main className="login-page"><div className="login-card">
    <div className="brand-mark"><CarFront size={28} /></div>
    <p className="eyebrow">VEHICLE RENTAL / ADMIN</p><h1>Welcome back.</h1><p className="muted">Sign in to manage your rental platform.</p>
    <form onSubmit={handleSubmit(async values => { setError(null); try { await signIn(values.identifier, values.password); onSuccess() } catch (e) { setError(e) } })}>
      <label>Email or phone<input autoComplete="username" autoFocus {...register('identifier')} aria-invalid={!!errors.identifier} /></label>
      {errors.identifier && <p className="field-error">{errors.identifier.message}</p>}
      <label>Password<input type="password" autoComplete="current-password" {...register('password')} aria-invalid={!!errors.password} /></label>
      {errors.password && <p className="field-error">{errors.password.message}</p>}
      {error != null && <Failure error={error} />}
      <button className="button primary full" disabled={isSubmitting}>{isSubmitting ? 'Signing in…' : 'Sign in'}<ArrowRight size={17} /></button>
    </form><div className="login-footer"><ShieldCheck size={15} /> Administrator access only</div>
  </div></main>
}

export function App() {
  const client = useQueryClient()
  const [hasSession, setHasSession] = useState(() => !!session.read())
  const identity = useQuery({ queryKey: ['identity'], queryFn: async () => (await api.get<Identity>('/users/me')).data, enabled: hasSession, retry: false })
  useEffect(() => {
    const expire = () => { setHasSession(false); client.clear() }
    window.addEventListener('admin-session-expired', expire)
    return () => window.removeEventListener('admin-session-expired', expire)
  }, [client])
  async function logout() {
    const request = signOut()
    setHasSession(false)
    client.clear()
    try { await request } catch { toast.warning('Signed out here. Server token revocation could not be confirmed.') }
  }
  if (!hasSession) return <Login onSuccess={() => { client.clear(); setHasSession(true) }} />
  if (identity.isPending) return <main className="login-page"><Loading /></main>
  if (identity.isError || identity.data.role !== 'Admin') return <main className="login-page"><div className="login-card"><h1>Access unavailable</h1><Failure error={identity.error ?? new Error('An administrator account is required.')} retry={() => void identity.refetch()} /><button className="button secondary" onClick={() => void logout()}>Back to sign in</button></div></main>
  return <div className="app-shell">
    <aside className="sidebar"><a className="brand" href="/"><span className="brand-mark"><CarFront size={23} /></span><span>Vehicle Rental<small>ADMIN CONSOLE</small></span></a>
      <p className="nav-label">WORKSPACE</p><nav aria-label="Main navigation">
        <NavLink to="/vehicles"><CarFront size={19} />Vehicles</NavLink>
        <NavLink to="/catalog"><Layers3 size={19} />Catalog</NavLink>
        <NavLink to="/users"><Users size={19} />Users</NavLink>
      </nav><div className="sidebar-footer"><ShieldCheck size={17} /><span>Administration</span></div>
    </aside>
    <div className="workspace"><header className="topbar"><span className="workspace-label">Operations workspace</span><div className="account"><span className="avatar">{identity.data.email.charAt(0).toUpperCase()}</span><span className="email">{identity.data.email}</span><button className="icon-button" title="Sign out" aria-label="Sign out" onClick={() => void logout()}><LogOut size={18} /></button></div></header>
      <main className="content"><Routes><Route path="/vehicles" element={<Vehicles />} /><Route path="/catalog" element={<Catalog />} /><Route path="/users" element={<UserAccess currentUserId={identity.data.userId} />} /><Route path="*" element={<Navigate to="/vehicles" replace />} /></Routes></main>
      <footer className="page-footer">Vehicle Rental<span>Admin portal</span></footer>
    </div>
  </div>
}

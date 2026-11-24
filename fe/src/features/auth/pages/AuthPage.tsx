import { Navigate, useSearchParams } from 'react-router-dom'
import { ROUTES } from '@/shared/constants'
import LoginPage from './LoginPage'
import RegisterPage from './RegisterPage'

/** Canonical G04 entry point. The existing forms stay reusable at legacy URLs. */
export default function AuthPage() {
  const [searchParams] = useSearchParams()
  const mode = searchParams.get('mode')
  if (mode === 'register') return <RegisterPage />
  if (!mode || mode === 'login') return <LoginPage />
  return <Navigate to={`${ROUTES.PROFILE_AUTH}?mode=login`} replace />
}

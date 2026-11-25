import { useMutation } from '@tanstack/react-query'
import { useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { authApi } from '../api/auth.api'
import { useAuth } from '@/shared/contexts/AuthContext'
import { getErrorMessage } from '@/shared/lib/query-client'
import { ROUTES } from '@/shared/constants'
import { CUSTOMER_ROLES } from '@/types/auth.types'
import type { LoginFormValues } from '../schemas/login.schema'

export type PortalChoice = 'admin' | 'owner' | 'customer'

export function useLogin(onPortalChoice?: (portal: PortalChoice) => void) {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  return useMutation({
    mutationFn: (data: LoginFormValues) => authApi.login(data),
    onSuccess: ({ token, user }) => {
      login(user, token.accessToken, token.refreshToken)
      const isAdminLogin = location.pathname === ROUTES.ADMIN_LOGIN
      const isOwnerLogin = location.pathname === ROUTES.OWNER_LOGIN
      const requested = (location.state as { from?: string | { pathname?: string; search?: string; hash?: string; state?: unknown } } | null)?.from
      const requestedPath = typeof requested === 'string' ? requested : requested?.pathname
      const requestedIsAdmin = !!requestedPath && (requestedPath === ROUTES.ADMIN || requestedPath.startsWith(`${ROUTES.ADMIN}/`) || requestedPath === '/dashboard' || requestedPath.startsWith('/dashboard/'))
      const requestedIsOwner = !!requestedPath && (requestedPath === ROUTES.OWNER || requestedPath.startsWith(`${ROUTES.OWNER}/`))

      const isCustomerLogin = !isAdminLogin && !isOwnerLogin

      if (isAdminLogin && user.role !== 'ADMIN') {
        onPortalChoice?.(user.role === 'OWNER' ? 'owner' : 'customer')
        return
      }

      if (isOwnerLogin && user.role !== 'OWNER') {
        onPortalChoice?.(user.role === 'ADMIN' ? 'admin' : 'customer')
        return
      }

      if (isCustomerLogin && user.role === 'ADMIN' && !requestedIsAdmin) {
        onPortalChoice?.('admin')
        return
      }

      if (isCustomerLogin && user.role === 'OWNER' && !requestedIsOwner) {
        onPortalChoice?.('owner')
        return
      }

      toast.success(`Welcome, ${user.fullName}!`)
      const defaultDestination = user.role === 'ADMIN'
        ? ROUTES.DASHBOARD
        : user.role === 'OWNER'
          ? ROUTES.OWNER_DASHBOARD
          : CUSTOMER_ROLES.includes(user.role) ? ROUTES.PROFILE : ROUTES.HOME
      const requestedMatchesRole = user.role === 'ADMIN'
        ? requestedIsAdmin
        : user.role === 'OWNER'
          ? requestedIsOwner
          : !requestedIsAdmin && !requestedIsOwner
      const destination = requestedPath && requestedMatchesRole ? requestedPath : defaultDestination

      if (typeof requested === 'object' && requested?.pathname && destination === requestedPath) {
        navigate({ pathname: destination, search: requested.search, hash: requested.hash }, { replace: true, state: requested.state })
      } else {
        navigate(destination, { replace: true })
      }
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })
}

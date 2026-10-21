import { useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { authApi } from '../api/auth.api'
import { useAuth } from '@/shared/contexts/AuthContext'
import { getErrorMessage } from '@/shared/lib/query-client'
import { ROUTES } from '@/shared/constants'
import { CUSTOMER_ROLES } from '@/types/auth.types'
import type { LoginFormValues } from '../schemas/login.schema'

export function useLogin() {
  const { login } = useAuth()
  const navigate = useNavigate()

  return useMutation({
    mutationFn: (data: LoginFormValues) => authApi.login(data),
    onSuccess: ({ token, user }) => {
      login(user, token.accessToken, token.refreshToken)
      toast.success(`Welcome, ${user.fullName}!`)
      const destination = CUSTOMER_ROLES.includes(user.role) ? ROUTES.SEARCH : ROUTES.DASHBOARD
      navigate(destination, { replace: true })
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })
}

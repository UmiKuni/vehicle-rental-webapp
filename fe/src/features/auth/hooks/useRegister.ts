import { useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { authApi } from '../api/auth.api'
import { useAuth } from '@/shared/contexts/AuthContext'
import { getErrorMessage } from '@/shared/lib/query-client'
import { ROUTES } from '@/shared/constants'
import type { RegisterRequest } from '@/types/auth.types'
import type { RegisterFormValues } from '../schemas/register.schema'

export function useRegister() {
  const { login } = useAuth()
  const navigate = useNavigate()

  return useMutation({
    mutationFn: (data: RegisterFormValues) => {
      // Exclude confirmPassword when calling backend API
      const payload: RegisterRequest = {
        username: data.username,
        email: data.email,
        fullName: data.fullName,
        phone: data.phone,
        password: data.password,
      }
      return authApi.register(payload)
    },
    onSuccess: ({ token, user }) => {
      login(user, token.accessToken, token.refreshToken)
      toast.success(`Registration successful! Welcome, ${user.fullName}`)
      navigate(ROUTES.PROFILE, { replace: true })
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })
}

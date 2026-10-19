import { useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { authApi } from '../api/auth.api'
import { useAuth } from '@/shared/contexts/AuthContext'
import { getErrorMessage } from '@/shared/lib/query-client'
import { ROUTES } from '@/shared/constants'
import type { RegisterFormValues } from '../schemas/register.schema'

export function useRegister() {
  const { login } = useAuth()
  const navigate = useNavigate()

  return useMutation({
    mutationFn: (data: RegisterFormValues) => {
      // Exclude confirmPassword when calling backend API
      const { confirmPassword: _, ...payload } = data
      return authApi.register(payload)
    },
    onSuccess: ({ token, user }) => {
      login(user, token.accessToken, token.refreshToken)
      toast.success(`Đăng ký thành công! Chào mừng ${user.fullName}`)
      navigate(ROUTES.PROFILE, { replace: true })
    },
    onError: (error) => {
      toast.error(getErrorMessage(error))
    },
  })
}

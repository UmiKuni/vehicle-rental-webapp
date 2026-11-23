import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { isAxiosError } from 'axios'
import type { ReactNode } from 'react'
import { normalizeUserRole } from '@/types/auth.types'
import type { AuthState, AuthUser } from '@/types/auth.types'
import { TOKEN_KEY, REFRESH_TOKEN_KEY } from '@/shared/constants'
import { safeBase64Decode } from '@/shared/utils'
import { authApi } from '@/features/auth/api/auth.api'
import { getErrorMessage } from '@/shared/lib/query-client'
import { toast } from 'sonner'

interface AuthContextValue extends AuthState {
  login: (user: AuthUser, accessToken: string, refreshToken: string) => void
  logout: () => Promise<void>
  updateUser: (user: AuthUser) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

function parseUserFromToken(): AuthUser | null {
  const token = localStorage.getItem(TOKEN_KEY)
  if (!token) return null
  try {
    const encodedPayload = token.split('.')[1]?.replace(/-/g, '+').replace(/_/g, '/')
    if (!encodedPayload) return null
    const paddedPayload = encodedPayload.padEnd(Math.ceil(encodedPayload.length / 4) * 4, '=')
    const jsonStr = safeBase64Decode(paddedPayload)
    const payload = JSON.parse(jsonStr)
    // Verify the token hasn't expired
    if (payload.exp && payload.exp * 1000 < Date.now()) {
      return null
    }
    // Clear sessions created by the old frontend mock adapter.
    if (payload.user) {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(REFRESH_TOKEN_KEY)
      return null
    }

    const email = payload.email ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ?? ''
    const role = normalizeUserRole(payload.role ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'])
    const id = payload.sub ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
    if (!id || !role || role === 'GUEST') return null

    return {
      id,
      username: email,
      email,
      fullName: email.split('@')[0] || 'Customer',
      role,
    }
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => parseUserFromToken())
  const [isLoading, setIsLoading] = useState(() => Boolean(
    localStorage.getItem(TOKEN_KEY) || localStorage.getItem(REFRESH_TOKEN_KEY)
  ))

  useEffect(() => {
    if (!localStorage.getItem(TOKEN_KEY) && !localStorage.getItem(REFRESH_TOKEN_KEY)) return
    let active = true
    authApi.getMe()
      .then((currentUser) => { if (active) setUser(currentUser) })
      .catch((error: unknown) => {
        if (!active) return
        const status = isAxiosError(error) ? error.response?.status : undefined
        if (status === 401 || status === 403) {
          localStorage.removeItem(TOKEN_KEY)
          localStorage.removeItem(REFRESH_TOKEN_KEY)
          setUser(null)
        }
      })
      .finally(() => { if (active) setIsLoading(false) })
    return () => { active = false }
  }, [])

  const login = useCallback(
    (newUser: AuthUser, accessToken: string, refreshToken: string) => {
      localStorage.setItem(TOKEN_KEY, accessToken)
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
      setUser(newUser)
      setIsLoading(false)
    },
    []
  )

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } catch (error) {
      toast.error(`Server sign-out failed: ${getErrorMessage(error)}`)
    } finally {
      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(REFRESH_TOKEN_KEY)
      setUser(null)
    }
  }, [])

  const updateUser = useCallback((updatedUser: AuthUser) => {
    setUser(updatedUser)
  }, [])

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: !!user, isLoading, login, logout, updateUser }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}

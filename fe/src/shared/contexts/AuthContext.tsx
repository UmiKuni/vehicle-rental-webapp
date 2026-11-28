import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
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
  const queryClient = useQueryClient()
  const sessionVersion = useRef(0)
  const [user, setUser] = useState<AuthUser | null>(() => parseUserFromToken())
  const [isLoading, setIsLoading] = useState(() => Boolean(
    localStorage.getItem(TOKEN_KEY) || localStorage.getItem(REFRESH_TOKEN_KEY)
  ))

  useEffect(() => {
    if (!localStorage.getItem(TOKEN_KEY) && !localStorage.getItem(REFRESH_TOKEN_KEY)) return
    let active = true
    const requestedSessionVersion = sessionVersion.current
    authApi.getMe()
      .then((currentUser) => { if (active && requestedSessionVersion === sessionVersion.current) setUser(currentUser) })
      .catch((error: unknown) => {
        if (!active || requestedSessionVersion !== sessionVersion.current) return
        const status = isAxiosError(error) ? error.response?.status : undefined
        if (status === 401 || status === 403) {
          localStorage.removeItem(TOKEN_KEY)
          localStorage.removeItem(REFRESH_TOKEN_KEY)
          queryClient.clear()
          setUser(null)
        }
      })
      .finally(() => { if (active) setIsLoading(false) })
    return () => { active = false }
  }, [queryClient])

  useEffect(() => {
    const handleStorage = (event: StorageEvent) => {
      if (event.key !== TOKEN_KEY || event.newValue !== null) return
      sessionVersion.current += 1
      localStorage.removeItem(REFRESH_TOKEN_KEY)
      queryClient.clear()
      setUser(null)
      setIsLoading(false)
    }
    window.addEventListener('storage', handleStorage)
    return () => window.removeEventListener('storage', handleStorage)
  }, [queryClient])

  const login = useCallback(
    (newUser: AuthUser, accessToken: string, refreshToken: string) => {
      sessionVersion.current += 1
      queryClient.clear()
      localStorage.setItem(TOKEN_KEY, accessToken)
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
      setUser(newUser)
      setIsLoading(false)
    },
    [queryClient]
  )

  const logout = useCallback(async () => {
    sessionVersion.current += 1
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
    const accessToken = localStorage.getItem(TOKEN_KEY)
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    try { sessionStorage.removeItem('customer-verification-draft') } catch { /* Storage may be unavailable. */ }
    try {
      sessionStorage.removeItem('vehicle-search-filters')
      sessionStorage.removeItem('vehicle-search-location')
    } catch { /* Storage may be unavailable. */ }
    queryClient.clear()
    setUser(null)
    setIsLoading(false)
    try {
      if (refreshToken && accessToken) await authApi.logout(refreshToken, accessToken)
    } catch (error) {
      toast.error(`Server sign-out failed: ${getErrorMessage(error)}`)
    }
  }, [queryClient])

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

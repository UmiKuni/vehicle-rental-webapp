import axios from 'axios'
import type { AxiosError, InternalAxiosRequestConfig } from 'axios'
import { TOKEN_KEY, REFRESH_TOKEN_KEY } from '@/shared/constants'

// Create axios instance
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080/api'

function getLoginPath(pathname: string) {
  if (pathname.startsWith('/admin')) return '/admin/login'
  if (pathname.startsWith('/owner')) return '/owner/login'
  return '/profile/auth'
}

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  },
})

// Track refresh state to avoid multiple refresh calls
let isRefreshing = false
let failedQueue: Array<{
  resolve: (token: string) => void
  reject: (err: unknown) => void
}> = []

function processQueue(error: unknown, token: string | null = null) {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error)
    } else {
      prom.resolve(token!)
    }
  })
  failedQueue = []
}

// REQUEST interceptor — attach access token
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem(TOKEN_KEY)
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`
    }
    if (typeof FormData !== 'undefined' && config.data instanceof FormData) {
      // Let the browser add the multipart boundary for file uploads.
      config.headers.delete('Content-Type')
    }
    return config
  },
  (error) => Promise.reject(error)
)

// RESPONSE interceptor — handle 401 and refresh token
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean }
    const requestPath = originalRequest.url ?? ''
    const isAuthRequest = /\/users\/(login|register|logout)$|\/auth\/(login|register)$/.test(requestPath)

    if (error.response?.status === 401 && !isAuthRequest && !originalRequest._retry) {
      const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)

      if (!refreshToken) {
        // No refresh token — redirect to login
        localStorage.removeItem(TOKEN_KEY)
        localStorage.removeItem(REFRESH_TOKEN_KEY)
        if (!window.location.pathname.endsWith('/login') && window.location.pathname !== '/login') {
          const loginPath = getLoginPath(window.location.pathname)
          window.location.href = loginPath
        }
        return Promise.reject(error)
      }

      if (isRefreshing) {
        // Queue this request until the refresh completes
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject })
        })
          .then((token) => {
            originalRequest.headers.Authorization = `Bearer ${token}`
            return apiClient(originalRequest)
          })
          .catch((err) => Promise.reject(err))
      }

      originalRequest._retry = true
      isRefreshing = true

      try {
        const { data } = await axios.post(
          `${API_BASE_URL.replace(/\/$/, '')}/users/refresh`,
          { refreshToken },
          { headers: { 'Content-Type': 'application/json' } }
        )

        // The backend returns AuthResultDto directly. Accept an envelope too,
        // but always rotate both tokens together so the next refresh uses the
        // current refresh token.
        const authData = data.data ?? data
        const newAccessToken: unknown = authData?.accessToken
        const newRefreshToken: unknown = authData?.refreshToken
        if (typeof newAccessToken !== 'string' || !newAccessToken ||
            typeof newRefreshToken !== 'string' || !newRefreshToken) {
          throw new Error('The refresh response did not include both tokens.')
        }

        if (localStorage.getItem(REFRESH_TOKEN_KEY) !== refreshToken) {
          throw new Error('The active session changed while refreshing its token.')
        }

        localStorage.setItem(TOKEN_KEY, newAccessToken)
        localStorage.setItem(REFRESH_TOKEN_KEY, newRefreshToken)

        processQueue(null, newAccessToken)

        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`
        return apiClient(originalRequest)
      } catch (refreshError) {
        processQueue(refreshError, null)
        if (localStorage.getItem(REFRESH_TOKEN_KEY) === refreshToken) {
          localStorage.removeItem(TOKEN_KEY)
          localStorage.removeItem(REFRESH_TOKEN_KEY)
          const loginPath = getLoginPath(window.location.pathname)
          if (window.location.pathname !== loginPath) window.location.href = loginPath
        }
        return Promise.reject(refreshError)
      } finally {
        isRefreshing = false
      }
    }

    return Promise.reject(error)
  }
)

export default apiClient

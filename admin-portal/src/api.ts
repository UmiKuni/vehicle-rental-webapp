import axios from 'axios'

export interface Tokens { accessToken: string; refreshToken: string }
export interface Identity { userId: string; email: string; role: string }
export interface Brand { brandId: string; name: string }
export interface Model { modelId: string; brandId: string; name: string }
export interface Vehicle {
  vehicleId: string; ownerId: string; plateNumber: string; brandName: string
  modelName: string; status: string; transmission: string; seats: number; address: string
}

const tokenKey = 'vehicle-rental-admin-session'
export const session = {
  read(): Tokens | null {
    try { return JSON.parse(sessionStorage.getItem(tokenKey) ?? 'null') as Tokens | null }
    catch { return null }
  },
  save(tokens: Tokens) { sessionStorage.setItem(tokenKey, JSON.stringify(tokens)) },
  clear() { sessionStorage.removeItem(tokenKey) },
}

const baseURL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080/api'
const transport = axios.create({ baseURL, timeout: 15000 })
export const api = axios.create({ baseURL, timeout: 15000 })
let refreshRequest: Promise<Tokens> | null = null

api.interceptors.request.use(config => {
  const tokens = session.read()
  if (tokens) config.headers.Authorization = `Bearer ${tokens.accessToken}`
  return config
})

api.interceptors.response.use(response => response, async (error: unknown) => {
  if (!axios.isAxiosError(error) || error.response?.status !== 401 || !error.config) throw error
  const config = error.config as typeof error.config & { retried?: boolean }
  const previous = session.read()
  if (!previous || config.retried) {
    session.clear()
    window.dispatchEvent(new Event('admin-session-expired'))
    throw error
  }
  config.retried = true
  try {
    refreshRequest ??= transport.post<Tokens>('/users/refresh', { refreshToken: previous.refreshToken })
      .then(({ data }) => {
        // A response arriving after sign-out must never recreate the session.
        if (session.read()?.refreshToken !== previous.refreshToken) throw new Error('Session ended.')
        session.save(data)
        return data
      }).finally(() => { refreshRequest = null })
    const tokens = await refreshRequest
    config.headers.Authorization = `Bearer ${tokens.accessToken}`
    return await api.request(config)
  } catch (refreshError) {
    session.clear()
    window.dispatchEvent(new Event('admin-session-expired'))
    throw refreshError
  }
})

export async function signIn(identifier: string, password: string) {
  const { data: tokens } = await transport.post<Tokens>('/users/login', { identifier, password })
  try {
    const { data: user } = await transport.get<Identity>('/users/me', {
      headers: { Authorization: `Bearer ${tokens.accessToken}` },
    })
    if (user.role !== 'Admin') throw new Error('This account does not have admin access.')
    session.save(tokens)
    return user
  } catch (error) {
    await transport.post('/users/logout', { refreshToken: tokens.refreshToken }, {
      headers: { Authorization: `Bearer ${tokens.accessToken}` },
    }).catch(() => undefined)
    throw error
  }
}

export async function signOut() {
  const tokens = session.read()
  session.clear()
  if (tokens) await transport.post('/users/logout', { refreshToken: tokens.refreshToken }, {
    headers: { Authorization: `Bearer ${tokens.accessToken}` },
  })
}

export function errorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 401) return 'Your session expired or the credentials are incorrect.'
    if (error.response?.status === 403) return 'You do not have permission to perform this action.'
    if (!error.response) return 'Cannot reach the API. Check your connection and try again.'
    const data: unknown = error.response.data
    if (typeof data === 'string' && data.length < 400 && !data.includes('<')) return data
    if (data && typeof data === 'object') {
      const problem = data as { detail?: string; title?: string; error?: string }
      return problem.detail || problem.error || problem.title || 'The request failed. Please try again.'
    }
  }
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.'
}

import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { toast } from 'sonner'
import { useAuth } from '@/shared/contexts/AuthContext'
import { TOKEN_KEY } from '@/shared/constants'

export interface RealtimeNotification {
  notificationId: string
  type: string
  content: string
  occurredAt: string
}

type ConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'
interface NotificationRealtimeValue {
  notifications: RealtimeNotification[]
  connectionState: ConnectionState
}

const NotificationRealtimeContext = createContext<NotificationRealtimeValue | null>(null)
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080/api'
const HUB_URL = `${API_BASE_URL.replace(/\/api\/?$/, '').replace(/\/$/, '')}/hubs/notifications`

export function NotificationRealtimeProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const userId = user?.id
  const ownerId = userId === undefined || userId === null ? null : String(userId)
  const [received, setReceived] = useState<Array<RealtimeNotification & { ownerId: string }>>([])
  const [connection, setConnection] = useState<{ ownerId: string | null; state: ConnectionState }>({ ownerId: null, state: 'disconnected' })

  useEffect(() => {
    if (!ownerId) return

    let active = true
    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? '' })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()

    connection.on('ReceiveNotification', (payload: unknown) => {
      if (!payload || typeof payload !== 'object') return
      const value = payload as Partial<RealtimeNotification>
      if (typeof value.notificationId !== 'string' || typeof value.content !== 'string' || typeof value.type !== 'string') return
      const notification: RealtimeNotification = {
        notificationId: value.notificationId,
        content: value.content,
        type: value.type,
        occurredAt: typeof value.occurredAt === 'string' ? value.occurredAt : new Date().toISOString(),
      }
      setReceived((current) => [{ ...notification, ownerId }, ...current.filter((item) => item.notificationId !== notification.notificationId || item.ownerId !== ownerId)].slice(0, 100))
      toast.info(notification.content, { id: notification.notificationId })
    })
    connection.onreconnecting(() => { if (active) setConnection({ ownerId, state: 'reconnecting' }) })
    connection.onreconnected(() => { if (active) setConnection({ ownerId, state: 'connected' }) })
    connection.onclose(() => { if (active) setConnection({ ownerId, state: 'disconnected' }) })

    const retryDelays = [0, 2_000, 5_000, 10_000, 30_000]
    const startWithRetry = async () => {
      let attempt = 0
      while (active && connection.state === 'Disconnected') {
        const delay = retryDelays[Math.min(attempt, retryDelays.length - 1)]
        if (delay > 0) {
          setConnection({ ownerId, state: 'reconnecting' })
          await new Promise((resolve) => setTimeout(resolve, delay))
        }
        if (!active) return

        try {
          await connection.start()
          if (active) setConnection({ ownerId, state: 'connected' })
          return
        } catch {
          attempt += 1
        }
      }
    }

    void startWithRetry()

    return () => {
      active = false
      void connection.stop()
    }
  }, [ownerId])

  const notifications = useMemo(() => received.filter((item) => item.ownerId === ownerId), [received, ownerId])
  const connectionState: ConnectionState = !ownerId ? 'disconnected' : connection.ownerId === ownerId ? connection.state : 'connecting'
  const value = useMemo(() => ({ notifications, connectionState }), [notifications, connectionState])
  return <NotificationRealtimeContext.Provider value={value}>{children}</NotificationRealtimeContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useRealtimeNotifications() {
  const context = useContext(NotificationRealtimeContext)
  if (!context) throw new Error('useRealtimeNotifications must be used within NotificationRealtimeProvider')
  return context
}

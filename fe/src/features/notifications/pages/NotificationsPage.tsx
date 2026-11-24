import { Link } from 'react-router-dom'
import { ArrowLeft, Bell, BellRing } from 'lucide-react'
import { ROUTES } from '@/shared/constants'
import { useRealtimeNotifications } from '@/shared/contexts/NotificationRealtimeContext'

export default function NotificationsPage() {
  const { notifications, connectionState } = useRealtimeNotifications()
  const stateLabel = connectionState === 'connected' ? 'Live' : connectionState === 'reconnecting' ? 'Reconnecting' : connectionState === 'connecting' ? 'Connecting' : 'Offline'

  return (
    <main className="space-y-5 px-4 pt-5 pb-28">
      <Link to={ROUTES.HOME} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> Home</Link>
      <div className="flex items-start justify-between"><div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Inbox</p><h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">Notifications</h1><p className="mt-1 text-sm text-muted-foreground">Updates about your trips and account.</p></div><div className="flex size-11 items-center justify-center rounded-2xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><BellRing size={21} /></div></div>
      <div className="flex items-center justify-between rounded-xl border border-border bg-card px-4 py-3"><span className="text-xs text-muted-foreground">Real-time connection</span><span className={`rounded-full px-2.5 py-1 text-[10px] font-bold ${connectionState === 'connected' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'}`}>{stateLabel}</span></div>
      {notifications.length === 0 ? <section className="flex min-h-[42vh] flex-col items-center justify-center rounded-2xl border border-dashed border-border px-6 text-center">
        <div className="flex size-14 items-center justify-center rounded-2xl bg-muted text-muted-foreground"><Bell size={24} /></div>
        <h2 className="mt-4 text-base font-bold text-foreground">No live notifications yet</h2>
        <p className="mt-1 max-w-xs text-sm leading-6 text-muted-foreground">New in-app updates will appear here while you are connected.</p>
      </section> : <section aria-label="Recent live notifications" className="space-y-3">{notifications.map((notification) => <article key={notification.notificationId} className="flex gap-3 rounded-xl border border-border bg-card p-4"><div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><BellRing size={18} /></div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center justify-between gap-2"><p className="m-0 text-[10px] font-bold uppercase tracking-wider text-[#1a8a9d]">{notification.type}</p><time className="text-[10px] text-muted-foreground" dateTime={notification.occurredAt}>{new Date(notification.occurredAt).toLocaleString()}</time></div><p className="mb-0 mt-1 text-sm leading-6 text-foreground">{notification.content}</p></div></article>)}</section>}
      <p className="text-center text-[11px] leading-5 text-muted-foreground">The current backend exposes real-time delivery but no inbox history endpoint. Notifications sent while offline cannot be loaded here yet.</p>
    </main>
  )
}

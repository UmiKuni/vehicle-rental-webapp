import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, RefreshCw, Search, ShieldAlert } from 'lucide-react'
import { auditApi } from '@/features/admin/api/audit.api'
import type { AuditEntry, AuditFilters } from '@/features/admin/api/audit.api'
import { getErrorMessage } from '@/shared/lib/query-client'

const emptyFilters: AuditFilters = { page: 1, pageSize: 20 }
const inputClass = 'h-10 w-full rounded-lg border border-[#d8e2e1] bg-white px-3 text-sm text-[#243331] outline-none focus:border-[#168a8c] focus:ring-2 focus:ring-[#168a8c]/10'

function StateValue({ value }: { value: string | null }) {
  if (!value) return <span className="text-[#879492]">No snapshot</span>
  let formatted = value
  try {
    formatted = JSON.stringify(JSON.parse(value), null, 2)
  } catch { /* Show raw snapshots if the stored value is not valid JSON. */ }
  return <pre className="max-h-64 overflow-auto whitespace-pre-wrap break-all rounded-lg bg-[#101719] p-3 text-[11px] leading-5 text-[#d8e5e1]">{formatted}</pre>
}

function AuditDetails({ entry }: { entry: AuditEntry }) {
  const [historyMode, setHistoryMode] = useState<'entity' | 'actor' | null>(null)
  const detail = useQuery({ queryKey: ['admin', 'audit', entry.id], queryFn: () => auditApi.getById(entry.id) })
  const history = useQuery({
    queryKey: ['admin', 'audit-history', historyMode, entry.entityName, entry.entityId, entry.actorId],
    queryFn: () => historyMode === 'actor' ? auditApi.getActorHistory(entry.actorId) : auditApi.getEntityHistory(entry.entityName, entry.entityId),
    enabled: historyMode !== null,
  })
  const value = detail.data ?? entry
  if (detail.isError) return <p role="alert" className="text-xs text-red-700">Could not load the full entry: {getErrorMessage(detail.error)}</p>
  return <div className="space-y-4">
    <div className="grid gap-4 xl:grid-cols-2">
      <div><p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-[#788885]">Before</p><StateValue value={value.beforeState} /></div>
      <div><p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-[#788885]">After</p><StateValue value={value.afterState} /></div>
    </div>
    <div className="flex flex-wrap gap-2 border-t border-[#e3eae8] pt-3">
      <button type="button" onClick={() => setHistoryMode('entity')} className="rounded-md border border-[#d8e2e1] px-2.5 py-1.5 text-[10px] font-bold text-[#286d6c]">Related entity history</button>
      <button type="button" onClick={() => setHistoryMode('actor')} className="rounded-md border border-[#d8e2e1] px-2.5 py-1.5 text-[10px] font-bold text-[#286d6c]">Actor history</button>
      {historyMode && <button type="button" onClick={() => setHistoryMode(null)} className="rounded-md px-2.5 py-1.5 text-[10px] font-bold text-[#687a76]">Hide history</button>}
    </div>
    {historyMode && <div className="rounded-lg border border-[#e1e8e7] bg-white p-3">
      <p className="mb-2 text-[10px] font-bold uppercase tracking-wider text-[#687a76]">{historyMode === 'actor' ? 'Actions by this actor' : `History for ${entry.entityName} ${entry.entityId}`}</p>
      {history.isPending ? <p className="m-0 text-xs text-[#71817e]">Loading related history…</p> : history.isError ? <p role="alert" className="m-0 text-xs text-red-700">Could not load related history: {getErrorMessage(history.error)}</p> : history.data?.length ? <ul className="m-0 max-h-52 space-y-2 overflow-auto p-0">{history.data.slice(0, 50).map((item) => <li key={item.id} className="flex flex-wrap justify-between gap-2 border-b border-[#edf1f0] pb-2 text-[11px]"><span><b>{item.action}</b> · {item.entityName}</span><time className="text-[#73817e]">{new Date(item.timestamp).toLocaleString()}</time></li>)}</ul> : <p className="m-0 text-xs text-[#71817e]">No related audit entries.</p>}
    </div>}
  </div>
}

export default function AuditLogPage() {
  const [draft, setDraft] = useState({ actorId: '', entityName: '', entityId: '', action: '', from: '', to: '' })
  const [filters, setFilters] = useState<AuditFilters>(emptyFilters)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const query = useQuery({ queryKey: ['admin', 'audit', filters], queryFn: () => auditApi.getPage(filters), staleTime: 15_000 })
  const page = query.data

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    setSelectedId(null)
    setFilters({
      ...emptyFilters,
      actorId: draft.actorId.trim() || undefined,
      entityName: draft.entityName.trim() || undefined,
      entityId: draft.entityId.trim() || undefined,
      action: draft.action.trim() || undefined,
      from: draft.from ? new Date(draft.from).toISOString() : undefined,
      to: draft.to ? new Date(draft.to).toISOString() : undefined,
    })
  }

  const updatePage = (pageNumber: number) => {
    setSelectedId(null)
    setFilters((current) => ({ ...current, page: pageNumber }))
  }

  return <div className="space-y-5">
    <header className="flex flex-wrap items-end justify-between gap-3">
      <div><p className="m-0 text-[10px] font-extrabold uppercase tracking-[.18em] text-[#168a8c]">Security & compliance</p><h1 className="mb-0 mt-1 text-2xl font-extrabold tracking-tight text-[#172321]">Audit log</h1><p className="mb-0 mt-1 text-sm text-[#72817e]">Review sensitive actions recorded by the backend.</p></div>
      <button type="button" onClick={() => void query.refetch()} disabled={query.isFetching} className="inline-flex h-10 items-center gap-2 rounded-lg border border-[#d8e2e1] bg-white px-3 text-xs font-bold text-[#405553] disabled:opacity-50"><RefreshCw size={14} className={query.isFetching ? 'animate-spin' : ''} />Refresh</button>
    </header>

    <form onSubmit={submit} className="grid gap-3 rounded-xl border border-[#e1e8e7] bg-white p-4 sm:grid-cols-2 xl:grid-cols-4">
      <label className="text-[11px] font-bold text-[#687a76]">Actor ID<input className={`${inputClass} mt-1.5 font-normal`} value={draft.actorId} onChange={(event) => setDraft({ ...draft, actorId: event.target.value })} placeholder="User GUID" /></label>
      <label className="text-[11px] font-bold text-[#687a76]">Entity<input className={`${inputClass} mt-1.5 font-normal`} value={draft.entityName} onChange={(event) => setDraft({ ...draft, entityName: event.target.value })} placeholder="Booking, User, Vehicle…" /></label>
      <label className="text-[11px] font-bold text-[#687a76]">Entity ID<input className={`${inputClass} mt-1.5 font-normal`} value={draft.entityId} onChange={(event) => setDraft({ ...draft, entityId: event.target.value })} /></label>
      <label className="text-[11px] font-bold text-[#687a76]">Action<input className={`${inputClass} mt-1.5 font-normal`} value={draft.action} onChange={(event) => setDraft({ ...draft, action: event.target.value })} placeholder="RefundPayment…" /></label>
      <label className="text-[11px] font-bold text-[#687a76]">From<input type="datetime-local" className={`${inputClass} mt-1.5 font-normal`} value={draft.from} onChange={(event) => setDraft({ ...draft, from: event.target.value })} /></label>
      <label className="text-[11px] font-bold text-[#687a76]">To<input type="datetime-local" className={`${inputClass} mt-1.5 font-normal`} value={draft.to} onChange={(event) => setDraft({ ...draft, to: event.target.value })} /></label>
      <div className="flex items-end sm:col-span-2 xl:col-span-2"><button type="submit" className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-lg bg-[#167b7d] px-4 text-xs font-bold text-white hover:bg-[#126a6c]"><Search size={15} />Apply filters</button></div>
    </form>

    {query.isError && <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">Could not load audit events: {getErrorMessage(query.error)} <button type="button" onClick={() => void query.refetch()} className="ml-2 font-bold underline">Retry</button></div>}
    {query.isPending && <div className="rounded-xl border border-[#e1e8e7] bg-white p-10 text-center text-sm text-[#71817e]">Loading audit events…</div>}
    {page && <>
      <section className="overflow-hidden rounded-xl border border-[#e1e8e7] bg-white">
        <div className="flex items-center justify-between border-b border-[#e8eeed] px-4 py-3"><p className="m-0 text-xs font-bold text-[#405553]">{page.totalCount.toLocaleString()} events</p><p className="m-0 text-[11px] text-[#758481]">Page {page.page} of {page.totalPages || 1}</p></div>
        {page.items.length === 0 ? <div className="flex min-h-56 flex-col items-center justify-center px-5 text-center"><ShieldAlert size={25} className="text-[#83918e]" /><h2 className="mb-0 mt-3 text-sm font-bold text-[#334340]">No matching audit events</h2><p className="mb-0 mt-1 text-xs text-[#7a8986]">Adjust the filters or check back after new actions are recorded.</p></div> : <div className="overflow-x-auto">
          <table className="w-full min-w-[900px] text-left text-xs">
            <thead className="bg-[#f5f8f7] text-[10px] uppercase tracking-wider text-[#71817e]"><tr><th className="px-4 py-3">Timestamp</th><th className="px-4 py-3">Action</th><th className="px-4 py-3">Entity</th><th className="px-4 py-3">Actor</th><th className="px-4 py-3">IP address</th><th className="px-4 py-3">Details</th></tr></thead>
            <tbody className="divide-y divide-[#edf1f0]">{page.items.map((entry) => <tr key={entry.id} className="align-top hover:bg-[#fbfcfc]">
              <td className="whitespace-nowrap px-4 py-3 text-[#52625f]">{new Date(entry.timestamp).toLocaleString()}</td>
              <td className="px-4 py-3 font-bold text-[#263633]">{entry.action}</td>
              <td className="px-4 py-3"><span className="font-bold text-[#3d514d]">{entry.entityName}</span><span className="mt-1 block max-w-56 truncate font-mono text-[10px] text-[#879390]" title={entry.entityId}>{entry.entityId}</span></td>
              <td className="px-4 py-3 font-mono text-[10px] text-[#667572]">{entry.actorId}</td>
              <td className="px-4 py-3 font-mono text-[10px] text-[#667572]">{entry.ipAddress}</td>
              <td className="px-4 py-3"><button type="button" aria-expanded={selectedId === entry.id} onClick={() => setSelectedId(selectedId === entry.id ? null : entry.id)} className="rounded-md border border-[#dce5e4] px-2.5 py-1.5 text-[10px] font-bold text-[#286d6c] hover:bg-[#eff8f7]">{selectedId === entry.id ? 'Hide' : 'View'}</button></td>
            </tr>).flatMap((row, index) => {
              const entry = page.items[index]
              return selectedId === entry.id ? [row, <tr key={`${entry.id}-detail`}><td colSpan={6} className="bg-[#f7faf9] px-4 py-4"><AuditDetails entry={entry} /></td></tr>] : [row]
            })}</tbody>
          </table>
        </div>}
        <footer className="flex items-center justify-between border-t border-[#e8eeed] px-4 py-3"><button type="button" disabled={page.page <= 1 || query.isFetching} onClick={() => updatePage(page.page - 1)} className="inline-flex h-8 items-center gap-1 rounded-md border border-[#d8e2e1] px-2.5 text-[11px] font-bold disabled:opacity-40"><ChevronLeft size={14} />Previous</button><span className="text-[10px] text-[#7c8987]">Showing {page.items.length} of {page.totalCount}</span><button type="button" disabled={page.page >= page.totalPages || query.isFetching} onClick={() => updatePage(page.page + 1)} className="inline-flex h-8 items-center gap-1 rounded-md border border-[#d8e2e1] px-2.5 text-[11px] font-bold disabled:opacity-40">Next<ChevronRight size={14} /></button></footer>
      </section>
      <p className="m-0 text-[10px] leading-5 text-[#84918e]">Audit snapshots may contain sensitive operational data. Access is restricted to administrators and personal numbers are masked by the backend.</p>
    </>}
  </div>
}

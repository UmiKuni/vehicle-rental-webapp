import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronRight, EyeOff, RefreshCw, Search, X } from 'lucide-react'
import { toast } from 'sonner'
import { z } from 'zod'
import { api, errorMessage } from './api'
import type { Vehicle } from './api'
import { Empty, Failure, Loading, Modal, Status } from './ui'

type Scope = { type: 'published' | 'vehicle' | 'owner'; id: string }
type Action = 'approve' | 'reject' | 'unpublish'
const actionLabels: Record<Action, string> = { approve: 'Approve vehicle', reject: 'Reject vehicle', unpublish: 'Unpublish vehicle' }

export function Vehicles() {
  const client = useQueryClient()
  const [scope, setScope] = useState<Scope>({ type: 'published', id: '' })
  const [lookupType, setLookupType] = useState<'vehicle' | 'owner'>('vehicle')
  const [lookup, setLookup] = useState('')
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<Vehicle | null>(null)
  const [action, setAction] = useState<Action | null>(null)
  const [reason, setReason] = useState('')
  const vehicles = useQuery({ queryKey: ['vehicles', scope], queryFn: async () => {
    if (scope.type === 'vehicle') return [(await api.get<Vehicle>(`/fleet/vehicles/${scope.id}`)).data]
    return (await api.get<Vehicle[]>(scope.type === 'owner' ? `/fleet/owners/${scope.id}/vehicles` : '/fleet/vehicles')).data
  } })
  const mutation = useMutation({ mutationFn: async ({ vehicle, action: verb, reason: note }: { vehicle: Vehicle; action: Action; reason: string }) => {
    await api.post(`/fleet/vehicles/${vehicle.vehicleId}/${verb}`, verb === 'approve' ? undefined : { reason: note.trim() })
  }, onSuccess: async () => {
    toast.success('Vehicle updated')
    setAction(null); setSelected(null); setReason('')
    await client.invalidateQueries({ queryKey: ['vehicles'] })
  } })
  const filtered = (vehicles.data ?? []).filter(vehicle => `${vehicle.plateNumber} ${vehicle.brandName} ${vehicle.modelName}`.toLowerCase().includes(search.toLowerCase()))
  function openAction(verb: Action) { mutation.reset(); setReason(''); setAction(verb) }
  return <>
    <div className="page-heading"><div><p className="eyebrow">FLEET OPERATIONS</p><h1>Vehicles</h1><p className="muted">Review listings and manage publication.</p></div><button className="button secondary" onClick={() => void vehicles.refetch()} disabled={vehicles.isFetching}><RefreshCw size={16} className={vehicles.isFetching ? 'spin' : ''} />Refresh</button></div>
    <div className="lookup-bar"><form onSubmit={event => {
      event.preventDefault()
      const id = lookup.trim()
      if (!z.string().uuid().safeParse(id).success) { toast.error('Enter a valid UUID.'); return }
      setScope({ type: lookupType, id }); setSearch('')
    }}><select aria-label="Lookup type" value={lookupType} onChange={e => setLookupType(e.target.value as 'vehicle' | 'owner')}><option value="vehicle">Vehicle ID</option><option value="owner">Owner ID</option></select><input aria-label="Lookup ID" placeholder="Paste an ID to find a listing" value={lookup} onChange={e => setLookup(e.target.value)} required /><button className="button primary" type="submit"><Search size={16} />Find</button></form></div>
    <section className="panel"><div className="panel-toolbar"><div className="panel-title">{scope.type === 'published' ? 'Published vehicles' : scope.type === 'owner' ? 'Owner’s vehicles' : 'Vehicle lookup'}<span className="count">{vehicles.data?.length ?? '—'}</span></div><div className="toolbar-actions">{scope.type !== 'published' && <button className="text-button" onClick={() => { setScope({ type: 'published', id: '' }); setLookup(''); setSearch('') }}>Show published</button>}<div className="search-input"><Search size={16} /><input aria-label="Filter vehicles" placeholder="Search plate or model" value={search} onChange={e => setSearch(e.target.value)} /></div></div></div>
      {vehicles.isPending ? <Loading /> : vehicles.isError ? <Failure error={vehicles.error} retry={() => void vehicles.refetch()} /> : filtered.length === 0 ? <Empty>No vehicles found.</Empty> : <div className="table-scroll"><table><thead><tr><th>Vehicle</th><th>Plate</th><th>Status</th><th>Seats</th><th><span className="sr-only">Review</span></th></tr></thead><tbody>{filtered.map(vehicle => <tr key={vehicle.vehicleId}><td><strong>{vehicle.brandName} {vehicle.modelName}</strong><small>{vehicle.transmission}</small></td><td><span className="plate">{vehicle.plateNumber}</span></td><td><Status value={vehicle.status} /></td><td>{vehicle.seats}</td><td className="align-right"><button className="icon-button" aria-label={`Review ${vehicle.plateNumber}`} title="Review vehicle" onClick={() => { setSelected(vehicle); setAction(null); mutation.reset() }}><ChevronRight size={20} /></button></td></tr>)}</tbody></table></div>}
    </section><p className="hint">Find pending or hidden listings using a vehicle or owner ID.</p>
    {selected && <Modal title={action ? actionLabels[action] : `${selected.brandName} ${selected.modelName}`} description={selected.plateNumber} onClose={() => { setSelected(null); setAction(null) }} busy={mutation.isPending}>
      {!action ? <><dl className="details"><div><dt>Status</dt><dd><Status value={selected.status} /></dd></div><div><dt>Vehicle ID</dt><dd className="mono">{selected.vehicleId}</dd></div><div><dt>Owner ID</dt><dd className="mono">{selected.ownerId}</dd></div><div><dt>Location</dt><dd>{selected.address}</dd></div><div><dt>Specification</dt><dd>{selected.seats} seats · {selected.transmission}</dd></div></dl><div className="dialog-actions">{selected.status === 'PendingApproval' && <><button className="button secondary" onClick={() => openAction('reject')}><X size={16} />Reject</button><button className="button primary" onClick={() => openAction('approve')}><Check size={16} />Approve</button></>}{selected.status === 'Published' && <button className="button danger" onClick={() => openAction('unpublish')}><EyeOff size={16} />Unpublish</button>}{!['Published', 'PendingApproval'].includes(selected.status) && <p className="muted">No review action is available for this status.</p>}</div></> : <form onSubmit={e => { e.preventDefault(); mutation.mutate({ vehicle: selected, action, reason }) }}>
        <p>{action === 'approve' ? 'Approve this listing and make it publicly visible?' : action === 'reject' ? 'Return this listing to draft for the owner to revise.' : 'Remove this listing from the published fleet.'}</p>
        {action !== 'approve' && <label>Reason<textarea value={reason} onChange={e => setReason(e.target.value)} required maxLength={1000} autoFocus rows={3} /></label>}
        {mutation.isError && <Failure error={new Error(errorMessage(mutation.error))} />}
        <div className="dialog-actions"><button type="button" className="button secondary" disabled={mutation.isPending} onClick={() => setAction(null)}>Back</button><button className={`button ${action === 'approve' ? 'primary' : 'danger'}`} disabled={mutation.isPending || (action !== 'approve' && !reason.trim())}>{mutation.isPending ? 'Saving…' : actionLabels[action]}</button></div>
      </form>}
    </Modal>}
  </>
}

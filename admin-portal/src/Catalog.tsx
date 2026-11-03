import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Search, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { api } from './api'
import type { Brand, Model } from './api'
import { Empty, Failure, Loading, Modal } from './ui'

type Editor = { type: 'brands' | 'models'; id?: string; name: string; brandId: string; remove?: boolean }

export function Catalog() {
  const client = useQueryClient()
  const [tab, setTab] = useState<'brands' | 'models'>('brands')
  const [search, setSearch] = useState('')
  const [editor, setEditor] = useState<Editor | null>(null)
  const brands = useQuery({ queryKey: ['brands'], queryFn: async () => (await api.get<Brand[]>('/fleet/brands')).data })
  const models = useQuery({ queryKey: ['models'], queryFn: async () => (await api.get<Model[]>('/fleet/models')).data })
  const mutation = useMutation({ mutationFn: async (value: Editor) => {
    const url = `/fleet/${value.type}${value.id ? `/${value.id}` : ''}`
    if (value.remove) await api.delete(url)
    else {
      const payload = value.type === 'models' ? { name: value.name.trim(), brandId: value.brandId } : { name: value.name.trim() }
      if (value.id) await api.put(url, payload)
      else await api.post(url, payload)
    }
  }, onSuccess: async (_, value) => {
    toast.success(`${value.type === 'brands' ? 'Brand' : 'Model'} ${value.remove ? 'deleted' : 'saved'}`)
    setEditor(null)
    await Promise.all([client.invalidateQueries({ queryKey: ['brands'] }), client.invalidateQueries({ queryKey: ['models'] }), client.invalidateQueries({ queryKey: ['vehicles'] })])
  } })
  function open(value: Editor) { mutation.reset(); setEditor(value) }
  const query = tab === 'brands' ? brands : models
  const rows = (tab === 'brands' ? (brands.data ?? []).map(b => ({ id: b.brandId, name: b.name, brandId: '' })) : (models.data ?? []).map(m => ({ id: m.modelId, name: m.name, brandId: m.brandId }))).filter(row => row.name.toLowerCase().includes(search.toLowerCase()))
  const singular = editor?.type === 'brands' ? 'brand' : 'model'
  return <>
    <div className="page-heading"><div><p className="eyebrow">FLEET REFERENCE</p><h1>Catalog</h1><p className="muted">Keep vehicle brands and models organized.</p></div><button className="button primary" onClick={() => open({ type: tab, name: '', brandId: '' })}><Plus size={17} />Add {tab === 'brands' ? 'brand' : 'model'}</button></div>
    <section className="panel"><div className="panel-toolbar"><div className="tabs" role="group" aria-label="Catalog type"><button className={tab === 'brands' ? 'active' : ''} aria-pressed={tab === 'brands'} onClick={() => { setTab('brands'); setSearch('') }}>Brands <span className="count">{brands.data?.length ?? '—'}</span></button><button className={tab === 'models' ? 'active' : ''} aria-pressed={tab === 'models'} onClick={() => { setTab('models'); setSearch('') }}>Models <span className="count">{models.data?.length ?? '—'}</span></button></div><div className="search-input"><Search size={16} /><input aria-label="Search catalog" placeholder="Search by name" value={search} onChange={e => setSearch(e.target.value)} /></div></div>
      {query.isPending ? <Loading /> : query.isError ? <Failure error={query.error} retry={() => void query.refetch()} /> : rows.length === 0 ? <Empty>No {tab} found. Add one to get started.</Empty> : <div className="table-scroll"><table><thead><tr><th>Name</th>{tab === 'models' && <th>Brand</th>}<th>ID</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{rows.map(row => <tr key={row.id}><td><strong>{row.name}</strong></td>{tab === 'models' && <td>{brands.data?.find(b => b.brandId === row.brandId)?.name ?? row.brandId}</td>}<td className="mono muted">{row.id}</td><td><div className="row-actions"><button className="icon-button" title="Edit" aria-label={`Edit ${row.name}`} onClick={() => open({ ...row, type: tab })}><Pencil size={16} /></button><button className="icon-button destructive" title="Delete" aria-label={`Delete ${row.name}`} onClick={() => open({ ...row, type: tab, remove: true })}><Trash2 size={16} /></button></div></td></tr>)}</tbody></table></div>}
    </section>
    {editor && <Modal title={`${editor.remove ? 'Delete' : editor.id ? 'Edit' : 'Add'} ${singular}`} description={editor.remove ? `Delete “${editor.name}”? This action cannot be undone.` : `Manage this ${singular} in the vehicle catalog.`} onClose={() => setEditor(null)} busy={mutation.isPending}>
      <form onSubmit={e => { e.preventDefault(); mutation.mutate(editor) }}>
        {!editor.remove && <><label>Name<input value={editor.name} onChange={e => setEditor({ ...editor, name: e.target.value })} required autoFocus maxLength={100} /></label>{editor.type === 'models' && <label>Brand<select required value={editor.brandId} onChange={e => setEditor({ ...editor, brandId: e.target.value })}><option value="">Select a brand</option>{brands.data?.map(brand => <option key={brand.brandId} value={brand.brandId}>{brand.name}</option>)}</select>{brands.isError && <Failure error={brands.error} retry={() => void brands.refetch()} />}{brands.data?.length === 0 && <span className="muted">Create a brand first.</span>}</label>}</>}
        {editor.remove && <p className="hint">Items in use by vehicles or models cannot be deleted.</p>}
        {mutation.isError && <Failure error={mutation.error} />}
        <div className="dialog-actions"><button type="button" className="button secondary" onClick={() => setEditor(null)} disabled={mutation.isPending}>Cancel</button><button className={`button ${editor.remove ? 'danger' : 'primary'}`} disabled={mutation.isPending || (!editor.remove && (!editor.name.trim() || (editor.type === 'models' && !editor.brandId)))}>{mutation.isPending ? 'Saving…' : editor.remove ? 'Delete' : 'Save'}</button></div>
      </form>
    </Modal>}
  </>
}

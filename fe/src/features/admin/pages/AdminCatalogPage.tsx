import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Layers3, Pencil, Plus, Search, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'
import apiClient from '@/shared/lib/axios'
import { Button } from '@/shared/components/ui/Button'
import { ErrorState } from '@/shared/components/common/StateViews'

interface Brand {
  brandId: string
  name: string
}
interface Model {
  modelId: string
  brandId: string
  name: string
}
type Kind = 'brands' | 'models'
interface Editor {
  kind: Kind
  id?: string
  name: string
  brandId: string
  remove?: boolean
}

export default function AdminCatalogPage() {
  const client = useQueryClient()
  const [kind, setKind] = useState<Kind>('brands')
  const [search, setSearch] = useState('')
  const [editor, setEditor] = useState<Editor | null>(null)
  const brands = useQuery({
    queryKey: ['admin', 'brands'],
    queryFn: async () => (await apiClient.get<Brand[]>('/fleet/brands')).data,
  })
  const models = useQuery({
    queryKey: ['admin', 'models'],
    queryFn: async () => (await apiClient.get<Model[]>('/fleet/models')).data,
  })
  const mutation = useMutation({
    mutationFn: async (value: Editor) => {
      const path = `/fleet/${value.kind}${value.id ? `/${value.id}` : ''}`
      if (value.remove) return apiClient.delete(path)
      const body =
        value.kind === 'models'
          ? { name: value.name.trim(), brandId: value.brandId }
          : { name: value.name.trim() }
      return value.id ? apiClient.put(path, body) : apiClient.post(path, body)
    },
    onSuccess: async (_data, value) => {
      toast.success(
        `${value.kind === 'brands' ? 'Brand' : 'Model'} ${value.remove ? 'deleted' : 'saved'}`
      )
      setEditor(null)
      await Promise.all([
        client.invalidateQueries({ queryKey: ['admin', 'brands'] }),
        client.invalidateQueries({ queryKey: ['admin', 'models'] }),
      ])
    },
    onError: () => toast.error('Could not save this catalog change.'),
  })
  const query = kind === 'brands' ? brands : models
  const items = (
    kind === 'brands'
      ? (brands.data ?? []).map((item) => ({ id: item.brandId, name: item.name, brandId: '' }))
      : (models.data ?? []).map((item) => ({
          id: item.modelId,
          name: item.name,
          brandId: item.brandId,
        }))
  ).filter((item) => item.name.toLowerCase().includes(search.toLowerCase()))
  if (brands.isError || models.isError)
    return (
      <ErrorState
        message="Could not load the vehicle catalog."
        onRetry={() => {
          void brands.refetch()
          void models.refetch()
        }}
      />
    )

  return (
    <div className="space-y-4">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
        <div>
          <p className="mb-1.5 text-[10px] font-extrabold tracking-[.14em] text-[#178b8d]">
            FLEET REFERENCE DATA
          </p>
          <h1 className="m-0 text-[27px] font-extrabold leading-tight tracking-[-.04em] text-[#172725] sm:text-[30px]">
            Vehicle catalog
          </h1>
          <p className="mb-0 mt-1.5 text-[14px] font-semibold text-[#64736f]">
            Keep makes and models consistent across vehicle listings.
          </p>
        </div>
        <Button
          className="font-bold"
          onClick={() => {
            mutation.reset()
            setEditor({ kind, name: '', brandId: '' })
          }}
        >
          <Plus size={16} /> Add {kind === 'brands' ? 'brand' : 'model'}
        </Button>
      </div>
      <section className="overflow-hidden rounded-2xl border border-[#e3e9e9] bg-white shadow-[0_3px_14px_#18312b08]">
        <div className="flex flex-col gap-3 border-b border-[#e8eeea] px-4 py-2.5 sm:flex-row sm:items-center sm:justify-between sm:px-5">
          <div
            className="flex gap-1 rounded-lg bg-[#f3f6f3] p-1"
            role="tablist"
            aria-label="Catalog type"
          >
            {(['brands', 'models'] as const).map((tab) => (
              <button
                key={tab}
                role="tab"
                aria-selected={kind === tab}
                onClick={() => {
                  setKind(tab)
                  setSearch('')
                }}
                className={`rounded-md px-3 py-1.5 text-sm font-bold capitalize transition ${kind === tab ? 'bg-white text-[#295d40] shadow-sm' : 'text-[#64736d] hover:text-[#29372e]'}`}
              >
                {tab}
                <span className="ml-2 font-mono text-xs font-bold text-[#68766d]">
                  {tab === 'brands' ? (brands.data?.length ?? '—') : (models.data?.length ?? '—')}
                </span>
              </button>
            ))}
          </div>
          <label className="relative m-0 flex items-center gap-2 sm:w-72">
            <Search size={15} className="absolute left-3 text-[#829087]" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={`Search ${kind}`}
              className="h-9 w-full rounded-lg border border-[#dce5df] pl-9 pr-3 text-sm font-medium text-[#263733] outline-none placeholder:font-normal focus:border-[#7ca58a]"
            />
          </label>
        </div>
        {query.isPending ? (
          <div className="space-y-2 p-4">
            {[0, 1, 2].map((item) => (
              <div key={item} className="h-12 animate-pulse rounded bg-[#f3f6f3]" />
            ))}
          </div>
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center py-12 text-center">
            <span className="grid size-10 place-items-center rounded-lg bg-[#f0f4f0] text-[#67816e]">
              <Layers3 size={18} />
            </span>
            <p className="mb-1 mt-3 text-sm font-semibold">No {kind} found</p>
            <p className="m-0 text-xs text-[#89948c]">Add an entry or adjust your search.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[540px] text-left text-sm">
              <thead className="bg-[#f7f9f7] text-xs font-bold uppercase tracking-[.08em] text-[#58675e]">
                <tr>
                  <th className="px-5 py-2.5">Name</th>
                  {kind === 'models' && <th className="px-4 py-2.5">Brand</th>}
                  <th className="px-4 py-2.5">ID</th>
                  <th className="px-5 py-2.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr
                    key={item.id}
                    className="border-t border-[#e9eeea] text-[#263630] hover:bg-[#f8faf8]"
                  >
                    <td className="px-5 py-2.5 text-sm font-bold text-[#1d2c27]">{item.name}</td>
                    {kind === 'models' && (
                      <td className="px-4 py-2.5 text-[13px] font-semibold text-[#52635a]">
                        {brands.data?.find((brand) => brand.brandId === item.brandId)?.name ??
                          item.brandId}
                      </td>
                    )}
                    <td
                      className="max-w-56 truncate px-4 py-2.5 font-mono text-[11px] font-semibold text-[#5c6b62]"
                      title={item.id}
                    >
                      {item.id}
                    </td>
                    <td className="px-5 py-2">
                      <div className="flex justify-end gap-1">
                        <button
                          aria-label={`Edit ${item.name}`}
                          title="Edit"
                          onClick={() => {
                            mutation.reset()
                            setEditor({ kind, id: item.id, name: item.name, brandId: item.brandId })
                          }}
                          className="grid size-8 place-items-center rounded-md text-[#596a60] hover:bg-[#eff4f0] hover:text-[#2a6844]"
                        >
                          <Pencil size={15} />
                        </button>
                        <button
                          aria-label={`Delete ${item.name}`}
                          title="Delete"
                          onClick={() => {
                            mutation.reset()
                            setEditor({
                              kind,
                              id: item.id,
                              name: item.name,
                              brandId: item.brandId,
                              remove: true,
                            })
                          }}
                          className="grid size-8 place-items-center rounded-md text-[#796965] hover:bg-red-50 hover:text-red-700"
                        >
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      {editor && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-[#14251c80] p-4">
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="catalog-editor-title"
            className="relative w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl"
          >
            <button
              onClick={() => setEditor(null)}
              aria-label="Close"
              className="absolute right-4 top-4 grid size-8 place-items-center rounded-lg text-[#7a867d] hover:bg-[#f2f5f2]"
            >
              <X size={17} />
            </button>
            <p className="mb-2 text-[10px] font-bold tracking-[.14em] text-[#849087]">CATALOG</p>
            <h2 id="catalog-editor-title" className="m-0 text-xl font-semibold">
              {editor.remove
                ? `Delete ${kind === 'brands' ? 'brand' : 'model'}?`
                : `${editor.id ? 'Edit' : 'Add'} ${kind === 'brands' ? 'brand' : 'model'}`}
            </h2>
            <p className="mt-2 text-sm text-[#7d8980]">
              {editor.remove
                ? `“${editor.name}” may be in use by other catalog entries.`
                : 'This entry will be available in vehicle listings.'}
            </p>
            {!editor.remove && (
              <div className="mt-5 space-y-4">
                <label className="block text-xs font-semibold">
                  Name
                  <input
                    autoFocus
                    value={editor.name}
                    onChange={(event) => setEditor({ ...editor, name: event.target.value })}
                    maxLength={100}
                    className="mt-2 block w-full rounded-lg border border-[#dfe6e0] px-3 py-2.5 text-sm outline-none focus:border-[#7ca58a]"
                    placeholder="Enter a name"
                  />
                </label>
                {kind === 'models' && (
                  <label className="block text-xs font-semibold">
                    Brand
                    <select
                      value={editor.brandId}
                      onChange={(event) => setEditor({ ...editor, brandId: event.target.value })}
                      className="mt-2 block w-full rounded-lg border border-[#dfe6e0] bg-white px-3 py-2.5 text-sm outline-none focus:border-[#7ca58a]"
                    >
                      <option value="">Select a brand</option>
                      {brands.data?.map((brand) => (
                        <option key={brand.brandId} value={brand.brandId}>
                          {brand.name}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
              </div>
            )}
            {mutation.isError && (
              <p role="alert" className="mt-3 text-xs text-red-700">
                The API could not complete this change. Check whether the item is still in use.
              </p>
            )}
            <div className="mt-6 flex justify-end gap-2">
              <Button variant="outline" onClick={() => setEditor(null)}>
                Cancel
              </Button>
              <Button
                variant={editor.remove ? 'destructive' : 'default'}
                loading={mutation.isPending}
                disabled={
                  !editor.remove && (!editor.name.trim() || (kind === 'models' && !editor.brandId))
                }
                onClick={() => mutation.mutate(editor)}
              >
                {editor.remove ? 'Delete' : 'Save changes'}
              </Button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}

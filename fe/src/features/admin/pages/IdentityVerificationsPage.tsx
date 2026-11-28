import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Clock3, FileImage, RefreshCw, ShieldCheck, X } from 'lucide-react'
import { toast } from 'sonner'
import apiClient from '@/shared/lib/axios'
import { resolvePrivateStorageUrl } from '@/shared/lib/storage-url'
import { Button } from '@/shared/components/ui/Button'

interface PendingVerification {
  userId: string
  email: string
  phone: string
  fullName: string
  documentId: string
  documentType: string
  storageKey: string
  status: string
  accessUrl?: string | null
}

interface ReviewRequest {
  document: PendingVerification
  approved: boolean
  rejectReason?: string
}

export default function IdentityVerificationsPage() {
  const queryClient = useQueryClient()
  const [rejecting, setRejecting] = useState<PendingVerification | null>(null)
  const [reason, setReason] = useState('')
  const [failedImages, setFailedImages] = useState<{ updatedAt: number; documentIds: Set<string> }>(() => ({ updatedAt: 0, documentIds: new Set() }))
  const documents = useQuery({
    queryKey: ['admin', 'identity-verifications'],
    queryFn: async () => (await apiClient.get<PendingVerification[]>('/users/verifications/pending')).data,
    staleTime: 0,
  })
  const hasImageFailed = (documentId: string) =>
    failedImages.updatedAt === documents.dataUpdatedAt && failedImages.documentIds.has(documentId)
  const review = useMutation({
    mutationFn: async ({ document, approved, rejectReason }: ReviewRequest) =>
      apiClient.put(`/users/${document.userId}/documents/${document.documentId}/verify`, {
        approved,
        rejectReason,
      }),
    onSuccess: async (_result, request) => {
      toast.success(request.approved ? 'Document approved' : 'Document rejected')
      setRejecting(null)
      setReason('')
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin', 'identity-verifications'] }),
        queryClient.invalidateQueries({ queryKey: ['admin', 'users'] }),
      ])
    },
    onError: () => toast.error('Could not review this document. Refresh and try again.'),
  })

  return (
    <div className="space-y-5">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="mb-1.5 text-[10px] font-extrabold tracking-[.14em] text-[#178b8d]">IDENTITY REVIEW</p>
          <h1 className="m-0 text-[27px] font-extrabold leading-tight tracking-[-.04em] text-[#172725] sm:text-[30px]">Pending documents</h1>
          <p className="mb-0 mt-1.5 text-sm font-medium text-[#65746d]">Review private identity documents submitted by customers.</p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void documents.refetch()} loading={documents.isFetching}>
          <RefreshCw size={15} /> Refresh
        </Button>
      </div>

      {documents.isError ? (
        <section className="rounded-2xl border border-rose-200 bg-white p-8 text-center">
          <p className="font-bold text-[#263630]">Could not load pending documents</p>
          <p className="mt-1 text-sm text-[#68776d]">Check the users API connection and your admin access.</p>
          <Button className="mt-4" variant="outline" onClick={() => void documents.refetch()}>Try again</Button>
        </section>
      ) : documents.isPending ? (
        <div className="grid gap-4 lg:grid-cols-2">{[0, 1].map((item) => <div key={item} className="h-80 animate-pulse rounded-2xl bg-white" />)}</div>
      ) : documents.data.length === 0 ? (
        <section className="rounded-2xl border border-[#e3e9e9] bg-white px-5 py-14 text-center">
          <span className="mx-auto grid size-12 place-items-center rounded-xl bg-[#eef5ef] text-[#39734f]"><ShieldCheck size={22} /></span>
          <h2 className="mb-0 mt-4 text-base font-bold text-[#20312c]">Everything is reviewed</h2>
          <p className="mb-0 mt-1 text-sm text-[#6f7d74]">There are no identity documents waiting for review.</p>
        </section>
      ) : (
        <div className="grid gap-4 xl:grid-cols-2">
          {documents.data.map((document) => (
            <article key={document.documentId} className="overflow-hidden rounded-2xl border border-[#e3e9e9] bg-white shadow-[0_3px_14px_#18312b08]">
              <div className="flex items-start justify-between gap-3 border-b border-[#e8eeea] px-5 py-4">
                <div className="flex min-w-0 items-start gap-3">
                  <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-[#edf4ef] text-[#39734f]"><FileImage size={18} /></span>
                  <div className="min-w-0">
                    <h2 className="m-0 truncate text-sm font-bold text-[#20312c]">{document.fullName || 'Name not provided'}</h2>
                    <p className="mb-0 mt-1 truncate text-xs text-[#718078]">{document.email} · {document.phone || 'No phone'}</p>
                  </div>
                </div>
                <span className="inline-flex shrink-0 items-center gap-1 rounded-md bg-amber-50 px-2 py-1 text-[11px] font-bold text-amber-800"><Clock3 size={13} />{document.documentType}</span>
              </div>
              <div className="grid min-h-56 place-items-center bg-[#f6f8f6] p-3">
                {document.accessUrl ? (
                  document.storageKey.toLowerCase().endsWith('.pdf') ? (
                    <div className="w-full space-y-2">
                      <iframe src={resolvePrivateStorageUrl(document.accessUrl)} title={`${document.documentType} PDF for ${document.fullName || document.email}`} className="h-80 w-full rounded-lg border-0 bg-white" />
                      <a href={resolvePrivateStorageUrl(document.accessUrl)} target="_blank" rel="noreferrer" className="text-xs font-semibold text-[#178b8d]">Open PDF in a new tab</a>
                    </div>
                  ) : (
                    <a href={resolvePrivateStorageUrl(document.accessUrl)} target="_blank" rel="noreferrer" className="block max-h-[420px] max-w-full">
                      {hasImageFailed(document.documentId) ? (
                        <p role="alert" className="max-w-sm rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-center text-sm text-amber-900">
                          This file is missing or its private link is invalid. Ask the customer to upload it again, then refresh this list.
                        </p>
                      ) : (
                        <img
                          src={resolvePrivateStorageUrl(document.accessUrl)}
                          alt={`${document.documentType} for ${document.fullName || document.email}`}
                          className="max-h-[420px] max-w-full rounded-lg object-contain"
                          onError={() => setFailedImages((current) => ({
                            updatedAt: documents.dataUpdatedAt,
                            documentIds: new Set(current.updatedAt === documents.dataUpdatedAt ? current.documentIds : []).add(document.documentId),
                          }))}
                        />
                      )}
                    </a>
                  )
                ) : <p className="px-4 text-center text-sm text-[#718078]">The private document link is unavailable. Refresh the list to request a new link.</p>}
              </div>
              <div className="flex flex-col gap-3 border-t border-[#e8eeea] px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
                <span className="min-w-0 truncate font-mono text-[10px] text-[#829087]" title={document.userId}>User {document.userId}</span>
                <div className="flex justify-end gap-2">
                  <Button size="sm" variant="outline" disabled={review.isPending} onClick={() => { setRejecting(document); setReason('') }}><X size={14} /> Reject</Button>
                  <Button size="sm" disabled={!document.accessUrl || hasImageFailed(document.documentId) || review.isPending} loading={review.isPending && review.variables?.document.documentId === document.documentId} onClick={() => review.mutate({ document, approved: true })}><Check size={14} /> Approve</Button>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}

      {rejecting && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-[#14251c80] p-4" onMouseDown={(event) => { if (event.target === event.currentTarget) setRejecting(null) }}>
          <section role="dialog" aria-modal="true" aria-labelledby="reject-document-title" className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl">
            <p className="mb-2 text-[10px] font-bold tracking-[.14em] text-[#849087]">IDENTITY REVIEW</p>
            <h2 id="reject-document-title" className="m-0 text-xl font-bold text-[#20312c]">Reject this document?</h2>
            <p className="mt-2 text-sm text-[#64736f]">Tell {rejecting.fullName || rejecting.email} what needs to be corrected.</p>
            <label className="mt-5 block text-xs font-semibold text-[#344239]">Reason
              <textarea autoFocus value={reason} onChange={(event) => setReason(event.target.value)} maxLength={500} rows={3} className="mt-2 block w-full rounded-lg border border-[#dfe6e0] p-3 text-sm outline-none focus:border-[#7ca58a]" />
            </label>
            {review.isError && <p role="alert" className="mt-3 text-xs text-red-700">The API could not complete this review. Please retry.</p>}
            <div className="mt-5 flex justify-end gap-2">
              <Button variant="outline" onClick={() => setRejecting(null)}>Cancel</Button>
              <Button variant="destructive" disabled={!reason.trim() || review.isPending} loading={review.isPending} onClick={() => review.mutate({ document: rejecting, approved: false, rejectReason: reason.trim() })}>Reject document</Button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}

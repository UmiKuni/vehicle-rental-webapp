import * as Dialog from '@radix-ui/react-dialog'
import { AlertCircle, Inbox, LoaderCircle, X } from 'lucide-react'
import type { ReactNode } from 'react'
import { errorMessage } from './api'

export function Loading() {
  return <div className="state" role="status"><LoaderCircle className="spin" size={22} /><span>Loading…</span></div>
}
export function Empty({ children }: { children: ReactNode }) {
  return <div className="state"><Inbox size={28} /><span>{children}</span></div>
}
export function Failure({ error, retry }: { error: unknown; retry?: () => void }) {
  return <div className="error" role="alert"><AlertCircle size={18} /><span>{errorMessage(error)}</span>{retry && <button className="button secondary" onClick={retry}>Retry</button>}</div>
}
export function Modal({ title, description, children, onClose, busy = false }: {
  title: string; description: string; children: ReactNode; onClose: () => void; busy?: boolean
}) {
  return <Dialog.Root open onOpenChange={open => { if (!open && !busy) onClose() }}>
    <Dialog.Portal><Dialog.Overlay className="overlay" /><Dialog.Content className="modal" onEscapeKeyDown={e => { if (busy) e.preventDefault() }} onPointerDownOutside={e => e.preventDefault()}>
      <Dialog.Title className="modal-title">{title}</Dialog.Title>
      <Dialog.Description className="muted">{description}</Dialog.Description>
      <Dialog.Close className="icon-button modal-close" aria-label="Close dialog" disabled={busy}><X size={18} /></Dialog.Close>
      {children}
    </Dialog.Content></Dialog.Portal>
  </Dialog.Root>
}
export function Status({ value }: { value: string }) {
  return <span className={`badge ${value === 'Published' ? 'green' : value === 'PendingApproval' ? 'amber' : ''}`}>{value === 'PendingApproval' ? 'Pending approval' : value}</span>
}

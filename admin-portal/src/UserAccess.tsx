import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Ban, ShieldCheck, UserRoundCheck } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { api } from './api'
import { Failure, Modal } from './ui'

const schema = z.object({ userId: z.string().trim().uuid('Enter a valid user UUID.'), action: z.enum(['ban', 'unban']) })
type AccessRequest = z.infer<typeof schema>

export function UserAccess({ currentUserId }: { currentUserId: string }) {
  const [confirmation, setConfirmation] = useState<AccessRequest | null>(null)
  const [receipt, setReceipt] = useState<AccessRequest | null>(null)
  const { register, handleSubmit, formState: { errors } } = useForm<AccessRequest>({ resolver: zodResolver(schema), defaultValues: { action: 'ban' } })
  const mutation = useMutation({ mutationFn: async (request: AccessRequest) => { await api.put(`/users/${request.userId}/${request.action}`) }, onSuccess: (_, request) => {
    setConfirmation(null); setReceipt(request)
    toast.success(request.action === 'ban' ? 'User access suspended' : 'User access restored')
  } })
  return <><div className="page-heading"><div><p className="eyebrow">ACCOUNT OPERATIONS</p><h1>Users</h1><p className="muted">Suspend or restore access to an account.</p></div></div>
    <section className="panel access-panel"><div className="section-icon"><ShieldCheck size={24} /></div><h2>Manage account access</h2><p className="muted">Enter the user’s ID and choose an action.</p>
      <form onSubmit={handleSubmit(values => {
        if (values.userId.toLowerCase() === currentUserId.toLowerCase()) { toast.error('You cannot change your own account access here.'); return }
        mutation.reset(); setConfirmation(values)
      })}><label>User ID<input placeholder="00000000-0000-0000-0000-000000000000" {...register('userId')} aria-invalid={!!errors.userId} /></label>{errors.userId && <p className="field-error">{errors.userId.message}</p>}
        <fieldset className="access-options"><legend className="sr-only">Access action</legend><label><input type="radio" value="ban" {...register('action')} /><Ban size={19} /><span>Suspend access<small>Block sign-in and API access</small></span></label><label><input type="radio" value="unban" {...register('action')} /><UserRoundCheck size={19} /><span>Restore access<small>Allow the account to sign in again</small></span></label></fieldset>
        <button className="button primary" type="submit">Review change</button>
      </form>
    </section><p className="hint">Account lookup by name and email is not available yet. Verify the ID before changing access.</p>
    {receipt && <div className="success-note" role="status"><ShieldCheck size={18} /><span>Access {receipt.action === 'ban' ? 'suspended' : 'restored'} for <span className="mono">{receipt.userId}</span>.</span></div>}
    {confirmation && <Modal title={confirmation.action === 'ban' ? 'Suspend this account?' : 'Restore this account?'} description="Confirm the exact user ID before continuing." onClose={() => setConfirmation(null)} busy={mutation.isPending}>
      <p className="id-block">{confirmation.userId}</p><p>{confirmation.action === 'ban' ? 'The user will lose access to their account.' : 'The user will be able to sign in again.'}</p>
      {mutation.isError && <Failure error={mutation.error} />}
      <div className="dialog-actions"><button className="button secondary" disabled={mutation.isPending} onClick={() => setConfirmation(null)}>Cancel</button><button className={`button ${confirmation.action === 'ban' ? 'danger' : 'primary'}`} disabled={mutation.isPending} onClick={() => mutation.mutate(confirmation)}>{mutation.isPending ? 'Saving…' : 'Confirm change'}</button></div>
    </Modal>}
  </>
}

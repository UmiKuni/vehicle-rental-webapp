import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, ArrowRight, Check, ShieldCheck } from 'lucide-react'
import { ROUTES } from '@/shared/constants'

export default function VerificationTermsPage() {
  const [accepted, setAccepted] = useState(false)
  const navigate = useNavigate()

  return (
    <main className="mx-auto w-full max-w-md space-y-5 px-4 pt-5 pb-28">
      <Link to={ROUTES.PROFILE} className="inline-flex items-center gap-2 text-xs font-semibold text-muted-foreground"><ArrowLeft size={15} /> My profile</Link>
      <div className="flex items-start gap-3">
        <div className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-[#1a8a9d]/10 text-[#1a8a9d]"><ShieldCheck size={24} /></div>
        <div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#1a8a9d]">Account verification</p><h1 className="mt-1 text-2xl font-bold tracking-tight text-foreground">Before you begin</h1><p className="mt-1 text-sm leading-6 text-muted-foreground">Verify your identity and driver's license to complete your rental profile.</p></div>
      </div>
      <section className="rounded-2xl border border-border bg-card p-4">
        <h2 className="text-sm font-bold text-foreground">What you'll need</h2>
        <ol className="mt-4 space-y-4">
          {[
            ['Identity document', 'A valid National ID or passport with clearly visible details.'],
            ["Driver's license", 'Clear photos of both sides of your license.'],
            ['Contact information', 'A local address and a reachable phone number.'],
          ].map(([title, description], index) => <li key={title} className="flex gap-3"><span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-bold text-foreground">{index + 1}</span><div><p className="text-sm font-semibold text-foreground">{title}</p><p className="mt-0.5 text-xs leading-5 text-muted-foreground">{description}</p></div></li>)}
        </ol>
      </section>
      <div className="rounded-xl border border-[#1a8a9d]/20 bg-[#1a8a9d]/5 p-3 text-xs leading-5 text-muted-foreground">Your information and document photos are used only to verify your rental account. Make sure all details are accurate and the images are yours.</div>
      <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-border bg-card p-3.5">
        <input type="checkbox" checked={accepted} onChange={(event) => setAccepted(event.target.checked)} className="mt-0.5 size-4 accent-[#1a8a9d]" />
        <span className="text-xs leading-5 text-foreground">I have read the instructions and agree to provide my information and documents for account verification.</span>
      </label>
      <button disabled={!accepted} onClick={() => navigate(ROUTES.VERIFY_IDENTITY)} className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-[#1a8a9d] text-sm font-bold text-white transition hover:bg-[#147485] disabled:cursor-not-allowed disabled:opacity-45">Start verification <ArrowRight size={17} /></button>
      <p className="flex items-center justify-center gap-1.5 text-[11px] text-muted-foreground"><Check size={13} className="text-[#1a8a9d]" />You can save your progress and finish later.</p>
    </main>
  )
}

import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Toaster } from 'sonner'
import './app/styles/globals.css'
import './shared/lib/mock-api' // Remove when real backend is ready
import { AppProviders } from './app/providers/AppProviders'
import { AppRouter } from './app/router'

const rootEl = document.getElementById('root')!

createRoot(rootEl).render(
  <StrictMode>
    <AppProviders>
      <AppRouter />
      <Toaster position="top-right" richColors closeButton expand={false} />
    </AppProviders>
  </StrictMode>
)

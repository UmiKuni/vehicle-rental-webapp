import { QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { queryClient } from '@/shared/lib/query-client'
import { ThemeProvider } from '@/shared/contexts/ThemeContext'
import { AuthProvider } from '@/shared/contexts/AuthContext'
import { NotificationRealtimeProvider } from '@/shared/contexts/NotificationRealtimeContext'

interface AppProvidersProps {
  children: ReactNode
}

export function AppProviders({ children }: AppProvidersProps) {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <AuthProvider>
          <NotificationRealtimeProvider>{children}</NotificationRealtimeProvider>
        </AuthProvider>
      </ThemeProvider>
    </QueryClientProvider>
  )
}

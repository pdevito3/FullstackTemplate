import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { NotFound } from '@/components/not-found'
import { ThemeProvider } from '@/components/theme-provider'
import { Toaster } from '@/components/toaster'
import { AuthKeys } from '@/domain/auth/apis/auth.keys'
// Connects the generated API clients to apiClient before the first request.
import { onSessionEnd } from '@/lib/api-client'
import { createQueryClient } from '@/lib/query-client'
import './index.css'

// Import the generated route tree
import { routeTree } from './routeTree.gen'

// Create a new router instance
const router = createRouter({ routeTree, defaultNotFoundComponent: NotFound })

// Register the router instance for type safety
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

const queryClient = createQueryClient()
// With no cancelRefetch, a check of the session that runs already is not sent again, so a 401 of /bff/user cannot loop.
onSessionEnd(() => void queryClient.invalidateQueries({ queryKey: AuthKeys.user() }, { cancelRefetch: false }))

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider defaultTheme="system" storageKey="ui-theme">
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        <Toaster />
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
)

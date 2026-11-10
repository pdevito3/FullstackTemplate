import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { NotFound } from '@/components/not-found'
import { PageError } from '@/components/page-error'
import { ThemeProvider } from '@/components/theme-provider'
import { Toaster } from '@/components/toaster'
import { AuthKeys } from '@/domain/auth/apis/auth.keys'
import { keepInviteTokenOutOfTheUrl } from '@/domain/invites/invite-link'
import { retryFailedMembership } from '@/domain/membership/apis/refresh-caller-membership'
// Connects the generated API clients to apiClient before the first request.
import { onUnauthorized } from '@/lib/api-client'
import { createQueryClient } from '@/lib/query-client'
import './index.css'

// Import the generated route tree
import { routeTree } from './routeTree.gen'

keepInviteTokenOutOfTheUrl()

// Create a new router instance
const router = createRouter({ routeTree, defaultNotFoundComponent: NotFound, defaultErrorComponent: PageError })

// Register the router instance for type safety
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

const queryClient = createQueryClient()
// With no cancelRefetch, the 401s of calls that run at the same time wait for one check of the session.
onUnauthorized(async () => {
  await queryClient.invalidateQueries({ queryKey: AuthKeys.user() }, { cancelRefetch: false })
  return queryClient.getQueryData(AuthKeys.user()) === null
})

// The banner of the layout stays mounted, and a page that does not read the membership does not ask again.
router.subscribe('onResolved', ({ pathChanged }) => {
  if (pathChanged) void retryFailedMembership(queryClient)
})

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

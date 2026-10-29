import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { expect, test } from 'vitest'
import { commands } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { routeTree } from '@/routeTree.gen'

test('a session answer that is not a list of claims shows that the membership check failed', async () => {
  // A dev server with no BFF answered each path with its index.html.
  await commands.answerRequests([
    {
      method: 'GET',
      path: '/bff/user',
      contentType: 'text/html',
      body: '<!doctype html><html><body><div id="root"></div></body></html>',
    },
  ])
  const router = createRouter({ routeTree, history: createMemoryHistory({ initialEntries: ['/'] }) })
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  const screen = await render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  const check = screen.getByRole('region', { name: 'Membership check' })
  await expect.element(check.getByText('We could not check whether you are a member of a tenant.')).toBeVisible()
  await expect.element(check.getByRole('button', { name: 'Try Again' })).toBeVisible()
  await expect.element(screen.getByText('You are not signed in.')).toBeVisible()
})

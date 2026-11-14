import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { afterEach, expect, test } from 'vitest'
import { commands, page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { InviteDto } from '@/api/generated/models/types/invite-dto'
import { routeTree } from '@/routeTree.gen'

import { InviteUserButton } from './components/invite-user-dialog'
import { forgetInviteToken } from './invite-link'

const token = 'A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8S9t0U1v'
const invite: InviteDto = {
  id: 'inv_1',
  tenantId: 'tnt_1',
  role: 'User',
  email: null,
  requireEmailMatch: false,
  expiresAt: '2099-01-15T12:00:00Z',
  status: 'Pending',
  isExpired: false,
  claimedByUserId: null,
  allowedActions: ['Revoke'],
  version: 1,
}

const cached = (client: QueryClient) =>
  JSON.stringify([
    client.getQueryCache().getAll().map(({ queryKey, state }) => ({ queryKey, data: state.data })),
    client.getMutationCache().getAll().map(({ options, state }) => ({ key: options.mutationKey, state })),
  ])

afterEach(forgetInviteToken)

test('@INV-2 the query cache has no token while the link shows, and no cache has it after the dialog closes', async () => {
  await commands.answerRequests([
    { method: 'POST', path: '/api/v1/invites', status: 201, body: { invite, token } },
    { method: 'GET', path: '/api/v1/invites', body: [] },
  ])
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  await render(
    <QueryClientProvider client={client}>
      <InviteUserButton />
    </QueryClientProvider>,
  )

  await page.getByRole('button', { name: 'Invite a user' }).click()
  await page.getByRole('button', { name: 'Create link' }).click()
  const link = page.getByRole('textbox', { name: 'Invite link' })
  await expect.element(link).toHaveValue(`${window.location.origin}/join#token=${token}`)

  expect(JSON.stringify(client.getQueryCache().getAll().map(({ queryKey, state }) => [queryKey, state.data]))).not.toContain(token)

  await page.getByRole('button', { name: 'Done' }).click()
  await expect.element(link).not.toBeInTheDocument()

  await expect.poll(() => cached(client)).not.toContain(token)
})

test('@INV-3 no cache has the token after a preview and a claim', async () => {
  sessionStorage.setItem('invite-token', token)
  await commands.answerRequests([
    {
      method: 'GET',
      path: '/bff/user',
      body: [
        { type: 'sub', value: 'ada' },
        { type: 'given_name', value: 'Ada' },
        { type: 'family_name', value: 'Lovelace' },
        { type: 'email', value: 'ada@example.com' },
      ],
    },
    { method: 'GET', path: '/api/v1/users/by-identifier/ada', status: 404, body: { type: 'about:blank', title: 'Not found', status: 404 } },
    { method: 'GET', path: '/api/v1/users/me', body: { identifier: 'ada', name: 'Ada Lovelace', canOpenJobDashboard: false } },
    {
      method: 'POST',
      path: '/api/v1/invites/preview',
      body: { tenantName: 'Acme', role: 'User', expiresAt: '2099-01-15T12:00:00Z', requireEmailMatch: false },
    },
    { method: 'POST', path: '/api/v1/invites/claim', status: 403, body: { type: 'about:blank', title: 'Refused', status: 403, detail: 'No.' } },
  ])
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createRouter({ routeTree, history: createMemoryHistory({ initialEntries: ['/join'] }) })
  await render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  await page.getByRole('textbox', { name: 'Username' }).fill('ada')
  expect(cached(client)).not.toContain(token)

  await page.getByRole('button', { name: 'Join tenant' }).click()
  await expect.element(page.getByText('No.', { exact: true })).toBeVisible()

  expect(cached(client)).not.toContain(token)
})

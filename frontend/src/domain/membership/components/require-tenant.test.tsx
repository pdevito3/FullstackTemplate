import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRootRoute, createRouter, RouterProvider } from '@tanstack/react-router'
import { expect, onTestFinished, test } from 'vitest'
import { commands } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { member, memberClaims, signedInMember } from '@/test/session'

import { RequireTenant } from './require-tenant'

const claims = { method: 'GET', path: '/bff/user', body: memberClaims } as const
const lookupPath = `/api/v1/users/by-identifier/${member.identifier}`

async function renderGate() {
  const router = createRouter({
    routeTree: createRootRoute({
      component: () => (
        <RequireTenant purpose="to add rooms.">
          <form aria-label="New room"><input aria-label="Name" /></form>
        </RequireTenant>
      ),
    }),
    history: createMemoryHistory(),
  })
  const queryClient = new QueryClient()
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

test('a member gets the form', async () => {
  await commands.answerRequests(signedInMember)

  const screen = await renderGate()

  await expect.element(screen.getByRole('form', { name: 'New room' })).toBeVisible()
  await expect.element(screen.getByRole('link', { name: 'Start a tenant' })).not.toBeInTheDocument()
})

test('a user with no tenant gets the way to start one in place of the form', async () => {
  await commands.answerRequests([claims, { method: 'GET', path: lookupPath, status: 404 }])

  const screen = await renderGate()

  await expect.element(screen.getByText('Start a tenant to add rooms.', { exact: true })).toBeVisible()
  await expect.element(screen.getByRole('link', { name: 'Start a tenant' })).toHaveAttribute('href', '/start-tenant')
  await expect.element(screen.getByRole('form', { name: 'New room' })).not.toBeInTheDocument()
})

test('shows no form and no prompt until the membership is known', async () => {
  await commands.holdRequests(lookupPath)
  onTestFinished(() => commands.releaseRequests(lookupPath))
  await commands.answerRequests(signedInMember)

  const screen = await renderGate()

  await expect.element(screen.getByRole('status', { name: 'Loading your account' })).toBeVisible()
  await expect.element(screen.getByRole('form', { name: 'New room' })).not.toBeInTheDocument()
  await expect.element(screen.getByRole('link', { name: 'Start a tenant' })).not.toBeInTheDocument()

  await commands.releaseRequests(lookupPath)

  await expect.element(screen.getByRole('form', { name: 'New room' })).toBeVisible()
})

test('a failed membership check keeps the form, because the check does not say that the user has no tenant', async () => {
  await commands.answerRequests([claims, { method: 'GET', path: lookupPath, status: 403, body: { status: 403, title: 'Forbidden', detail: 'No access.' } }])

  const screen = await renderGate()

  await expect.element(screen.getByRole('form', { name: 'New room' })).toBeVisible()
})

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { expect, test } from 'vitest'
import { commands, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { UserDto } from '@/api/generated/models/types/user-dto'
import { routeTree } from '@/routeTree.gen'
import { member, signedInMember } from '@/test/session'

const user = (fullName: string): UserDto => ({ ...member, id: fullName, fullName })

const pagination = (pageNumber: number, totalPages: number) => ({
  'x-pagination': JSON.stringify({
    totalCount: totalPages * 10,
    pageSize: 10,
    currentPageSize: 1,
    currentStartIndex: (pageNumber - 1) * 10 + 1,
    currentEndIndex: (pageNumber - 1) * 10 + 1,
    pageNumber,
    totalPages,
    hasPrevious: pageNumber > 1,
    hasNext: pageNumber < totalPages,
  }),
})

async function renderListAt(path: string) {
  await commands.answerRequests([
    ...signedInMember,
    { method: 'GET', path: `/api/v1/tenants/${member.tenantId}`, body: { id: member.tenantId, name: 'Acme', allowedActions: [], version: 1 } },
    { method: 'GET', path: '/api/v1/users', headers: pagination(1, 3), body: [user('Page One')] },
    { method: 'GET', path: '/api/v1/users', query: { pageNumber: '2' }, headers: pagination(2, 3), body: [user('Page Two')] },
  ])
  const router = createRouter({ routeTree, history: createMemoryHistory({ initialEntries: [path] }) })
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const screen = await render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { screen, router }
}

test('reads the page from the URL', async () => {
  const { screen } = await renderListAt('/users?page=2')

  await expect.element(screen.getByRole('link', { name: 'Page Two' })).toBeVisible()
  await expect.element(screen.getByText('Page 2 of 3')).toBeVisible()
})

test('puts the page in the URL, and Previous to the first page takes it out', async () => {
  const { screen, router } = await renderListAt('/users')
  await expect.element(screen.getByRole('link', { name: 'Page One' })).toBeVisible()

  await userEvent.click(screen.getByRole('button', { name: 'Next' }))

  await expect.element(screen.getByRole('link', { name: 'Page Two' })).toBeVisible()
  expect(router.state.location.search).toEqual({ page: 2 })

  await userEvent.click(screen.getByRole('button', { name: 'Previous' }))

  await expect.element(screen.getByRole('link', { name: 'Page One' })).toBeVisible()
  expect(router.state.location.search).toEqual({})
})

test('falls back to the first page for a page that the contract refuses', async () => {
  const { screen, router } = await renderListAt('/users?page=0')

  await expect.element(screen.getByRole('link', { name: 'Page One' })).toBeVisible()
  expect(router.state.location.search).toEqual({})
})

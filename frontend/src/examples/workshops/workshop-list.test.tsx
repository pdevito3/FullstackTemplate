import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { expect, onTestFinished, test } from 'vitest'
import { commands, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import { getWorkshopListQuerySearchSchema } from '@/api/generated/workshops/zod/get-workshop-list'
import { maxLengthOf } from '@/lib/api-contract'
import { routeTree } from '@/routeTree.gen'

const workshop = (title: string): WorkshopDto => ({
  id: title,
  venueId: 'v_1',
  title,
  startsAt: '2026-06-01T15:00:00+00:00',
  venueTimeZone: 'America/Chicago',
  capacity: 20,
  status: 'Draft',
  allowedActions: [],
  version: 1,
})

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
    { method: 'GET', path: '/bff/user', status: 204 },
    { method: 'GET', path: '/api/v1/workshops', headers: pagination(1, 3), body: [workshop('Page one')] },
    { method: 'GET', path: '/api/v1/workshops', query: { pageNumber: '2' }, headers: pagination(2, 3), body: [workshop('Page two')] },
    { method: 'GET', path: '/api/v1/workshops', query: { search: 'Kubb' }, headers: pagination(1, 1), body: [workshop('Kubb basics')] },
  ])
  onTestFinished(() => commands.answerRequests([]))
  const router = createRouter({ routeTree, history: createMemoryHistory({ initialEntries: [path] }) })
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const screen = await render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { screen, router, search: screen.getByRole('searchbox', { name: 'Search workshops' }) }
}

test('reads the search and the page from the URL', async () => {
  const { screen, search } = await renderListAt('/workshops?search=Kubb')

  await expect.element(screen.getByRole('link', { name: 'Kubb basics' })).toBeVisible()
  await expect.element(search).toHaveValue('Kubb')
})

test('puts the page in the URL, and a new search goes back to the first page', async () => {
  const { screen, router, search } = await renderListAt('/workshops')
  await expect.element(screen.getByRole('link', { name: 'Page one' })).toBeVisible()

  await userEvent.click(screen.getByRole('button', { name: 'Next' }))

  await expect.element(screen.getByRole('link', { name: 'Page two' })).toBeVisible()
  expect(router.state.location.search).toEqual({ page: 2 })
  await expect.element(screen.getByText('Page 2 of 3')).toBeVisible()

  await userEvent.fill(search, 'Kubb')

  await expect.element(screen.getByRole('link', { name: 'Kubb basics' })).toBeVisible()
  expect(router.state.location.search).toEqual({ search: 'Kubb' })
})

test('falls back to the first page for a page that the contract refuses', async () => {
  const { screen, router } = await renderListAt('/workshops?page=0')

  await expect.element(screen.getByRole('link', { name: 'Page one' })).toBeVisible()
  expect(router.state.location.search).toEqual({})
})

test('takes no more search text than the contract allows', async () => {
  const { search } = await renderListAt('/workshops')

  await expect.element(search).toHaveAttribute('maxlength', String(maxLengthOf(getWorkshopListQuerySearchSchema.unwrap())))
})

import { useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { expect, onTestFinished, test } from 'vitest'
import { commands, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { VenueDto } from '@/api/generated/models/types/venue-dto'
import type { Answer } from '@/test/answer'
import { getVenueListQuerySearchSchema } from '@/api/generated/venues/zod/get-venue-list'
import { maxLengthOf } from '@/lib/api-contract'

import { VenueKeys } from '../apis/venue.keys'
import { VenuePicker } from './venue-picker'

const venue = (n: number, name = `Venue ${n}`): VenueDto => ({ id: `v_${n}`, name, timeZone: 'UTC', version: 1 })
const firstPage = Array.from({ length: 10 }, (_, i) => venue(i + 1))
const offPage = venue(42, 'Zeta Hall')

const pagination = (pageNumber: number, totalPages: number) => ({
  'x-pagination': JSON.stringify({
    totalCount: totalPages * 10,
    pageSize: 10,
    currentPageSize: 10,
    currentStartIndex: (pageNumber - 1) * 10 + 1,
    currentEndIndex: pageNumber * 10,
    pageNumber,
    totalPages,
    hasPrevious: pageNumber > 1,
    hasNext: pageNumber < totalPages,
  }),
})

function Picker({ initial }: { initial: string }) {
  const [value, setValue] = useState(initial)
  return <VenuePicker value={value} onChange={setValue} />
}

const searchFails = { method: 'GET', path: '/api/v1/venues', status: 500, body: { title: 'Server error', status: 500 } } as const

async function renderPicker(initial = '', ...more: Answer[]) {
  await commands.answerRequests([
    { method: 'GET', path: '/api/v1/venues', headers: pagination(1, 2), body: firstPage },
    { method: 'GET', path: `/api/v1/venues/${firstPage[0].id}`, body: firstPage[0] },
    { method: 'GET', path: '/api/v1/venues', query: { pageNumber: '2' }, headers: pagination(2, 2), body: [venue(11)] },
    { method: 'GET', path: `/api/v1/venues/${offPage.id}`, body: offPage },
    { method: 'GET', path: '/api/v1/venues', query: { search: 'Zeta' }, headers: pagination(1, 1), body: [offPage] },
    ...more,
  ])
  onTestFinished(() => commands.answerRequests([]))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const screen = await render(
    // The list keeps 12 px from the edge of the page. A list wider than the page scrolls the page and closes.
    <div style={{ padding: 12 }}>
      <QueryClientProvider client={client}>
        <Picker initial={initial} />
      </QueryClientProvider>
    </div>,
  )
  return { screen, client, input: screen.getByRole('combobox', { name: 'Venue' }) }
}

test('shows a picked venue that is not on the first page of the search, by its name', async () => {
  const { screen, input } = await renderPicker(offPage.id)

  await expect.element(input).toHaveValue('Zeta Hall')

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))

  await expect.element(screen.getByRole('option', { name: 'Zeta Hall' })).toHaveAttribute('aria-selected', 'true')
  await expect.element(screen.getByRole('option', { name: 'Venue 1', exact: true })).toBeVisible()
})

test('finds a venue by a search on the Server, and keeps the pick when the search goes', async () => {
  const { screen, input } = await renderPicker()

  await userEvent.fill(input, 'Zeta')
  await expect.element(screen.getByRole('option', { name: 'Zeta Hall' })).toBeVisible()
  await userEvent.keyboard('{ArrowDown}{Enter}')

  await expect.element(input).toHaveValue('Zeta Hall')

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))

  await expect.element(screen.getByRole('option', { name: 'Zeta Hall' })).toHaveAttribute('aria-selected', 'true')
  await expect.element(screen.getByRole('option', { name: 'Venue 1', exact: true })).toBeVisible()
})

test('takes no more search text than the contract allows', async () => {
  const { input } = await renderPicker()

  await expect.element(input).toHaveAttribute('maxlength', String(maxLengthOf(getVenueListQuerySearchSchema.unwrap())))
})

test('says that the venues did not load when the search fails, and searches again when the list opens again', async () => {
  const { screen } = await renderPicker('', searchFails)
  const toggle = screen.getByRole('button', { name: /show suggestions/i })

  await userEvent.click(toggle)

  await expect.element(screen.getByText('The venues did not load. Close the list, then open it to try again.')).toBeVisible()
  await expect.element(screen.getByText('No venue found.')).not.toBeInTheDocument()

  await commands.answerRequests([{ method: 'GET', path: '/api/v1/venues', headers: pagination(1, 1), body: firstPage }])
  await userEvent.keyboard('{Escape}')
  await userEvent.click(toggle)

  await expect.element(screen.getByRole('option', { name: 'Venue 1', exact: true })).toBeVisible()
})

test.each([
  ['a pick from a link', offPage.id, offPage],
  ['a pick from the first page', firstPage[0].id, firstPage[0]],
])('shows the new name of %s after the detail query reads the record again', async (_, initial, picked) => {
  const { client, input } = await renderPicker(initial)
  await expect.element(input).toHaveValue(picked.name)
  await commands.answerRequests([
    { method: 'GET', path: '/api/v1/venues', headers: pagination(1, 1), body: firstPage },
    { method: 'GET', path: `/api/v1/venues/${picked.id}`, body: { ...picked, name: 'Renamed Hall', version: 2 } },
  ])

  await client.invalidateQueries({ queryKey: VenueKeys.detail(picked.id) })

  await expect.element(input).toHaveValue('Renamed Hall')
})

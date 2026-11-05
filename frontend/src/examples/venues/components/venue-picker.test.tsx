import { useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { expect, onTestFinished, test, vi } from 'vitest'
import { commands, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { VenueDto } from '@/api/generated/models/types/venue-dto'
import type { Answer } from '@/test/answer'
import { observersHaveRun } from '@/test/observers-have-run'
import { venueForCreationDtoSchema } from '@/api/generated/models/zod/venue-for-creation-dto'
import { Notification } from '@/components/notification'
// The toasts of a failed request come from this client, and this import connects the generated clients to it.
import '@/lib/api-client'
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
    { method: 'GET', path: '/api/v1/venues', query: { filters: 'name @=* "Zeta"' }, headers: pagination(1, 1), body: [offPage] },
    ...more,
  ])
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const submitted = vi.fn((e: React.FormEvent) => e.preventDefault())
  const screen = await render(
    // The list keeps 12 px from the edge of the page. A list wider than the page scrolls the page and closes.
    <form style={{ padding: 12 }} onSubmit={submitted}>
      <QueryClientProvider client={client}>
        <Picker initial={initial} />
      </QueryClientProvider>
      <button type="submit">Save</button>
    </form>,
  )
  return { screen, client, submitted, input: screen.getByRole('combobox', { name: 'Venue' }) }
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

test('takes no more search text than a venue name can hold', async () => {
  const { input } = await renderPicker()

  await expect.element(input).toHaveAttribute('maxlength', String(maxLengthOf(venueForCreationDtoSchema.shape.name)))
})

test('says that the venues did not load when the search fails, and searches again when the list opens again', async () => {
  const toast = spyOnToasts()
  const { screen } = await renderPicker('', searchFails)
  const toggle = screen.getByRole('button', { name: /show suggestions/i })

  await userEvent.click(toggle)

  await expect.element(screen.getByText('The venues did not load. Close the list, then open it to try again.')).toBeVisible()
  await expect.element(screen.getByText('No venue found.')).not.toBeInTheDocument()
  expect(toast).toHaveBeenCalled()

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

const optionTexts = (screen: Awaited<ReturnType<typeof renderPicker>>['screen']) =>
  screen.getByRole('option').elements().map((option) => option.textContent)

const venuePage = (pageNumber: number, totalPages: number, body: VenueDto[]): Answer => ({
  method: 'GET',
  path: '/api/v1/venues',
  query: { pageNumber: String(pageNumber) },
  headers: pagination(pageNumber, totalPages),
  body,
})

// A venue that the Server adds between two loads moves the last venue of page 1 to the start of page 2.
test('shows a venue one time when page 2 starts with the last venue of page 1', async () => {
  const { screen } = await renderPicker('', venuePage(2, 2, [venue(10), venue(11)]))

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  expect(optionTexts(screen)).toEqual([...firstPage.map((row) => row.name), 'Venue 11'])
})

test('loads the next page when a page has only venues that the list already shows', async () => {
  const { screen } = await renderPicker('', venuePage(2, 3, firstPage), venuePage(3, 3, [venue(11)]))

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  expect(optionTexts(screen)).toEqual([...firstPage.map((row) => row.name), 'Venue 11'])
})

const pageOnePath = '/api/v1/venues?pageNumber=1&pageSize=10'
const pageTwoPath = '/api/v1/venues?pageNumber=2&pageSize=10'

/** Counts each request for `path` that the page sends from now on, in the page, so a request needs no trip to Node to count. */
function countRequests(path: string) {
  const open = vi.spyOn(XMLHttpRequest.prototype, 'open')
  onTestFinished(() => open.mockRestore())
  return () => open.mock.calls.filter(([, url]) => String(url).endsWith(path)).length
}

async function holdPage(path: string) {
  await commands.holdRequests(path)
  onTestFinished(() => commands.releaseRequests(path))
}

test('sends one request for page 2 while the answer is on the way, and shows the answer', async () => {
  await holdPage(pageTwoPath)
  const { screen } = await renderPicker()
  const pageTwoRequests = countRequests(pageTwoPath)

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByText('Loading more')).toBeVisible()
  await observersHaveRun()

  expect(pageTwoRequests()).toBe(1)

  await commands.releaseRequests(pageTwoPath)

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  expect(pageTwoRequests()).toBe(1)
})

test('stops the request for page 2 on a new search, and shows only the venues of the new search', async () => {
  await holdPage(pageTwoPath)
  const { screen, input } = await renderPicker()
  const stop = vi.spyOn(XMLHttpRequest.prototype, 'abort')
  onTestFinished(() => stop.mockRestore())

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByText('Loading more')).toBeVisible()

  await userEvent.fill(input, 'Zeta')

  await expect.element(screen.getByRole('option', { name: 'Zeta Hall' })).toBeVisible()
  expect(stop).toHaveBeenCalledOnce()

  await commands.releaseRequests(pageTwoPath)

  await expect.element(screen.getByRole('option')).toHaveTextContent('Zeta Hall')
})

test('loads page 2 when the list opens while page 1 loads again', async () => {
  const { screen, client } = await renderPicker()
  await expect.poll(() => client.isFetching()).toBe(0)
  await holdPage(pageOnePath)
  void client.invalidateQueries({ queryKey: VenueKeys.lists() })
  await expect.poll(() => client.isFetching()).toBe(1)

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', { name: 'Venue 1', exact: true })).toBeVisible()
  await observersHaveRun()
  await commands.releaseRequests(pageOnePath)

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
})

const pageTwoFails = { method: 'GET', path: '/api/v1/venues', query: { pageNumber: '2' }, status: 500, body: { title: 'Server error', status: 500 } } as const
const pageTwoWorks = { method: 'GET', path: '/api/v1/venues', query: { pageNumber: '2' }, headers: pagination(2, 2), body: [venue(11)] } as const
const failedRow = { name: 'More venues did not load. Try again' }

function spyOnToasts() {
  const toast = vi.spyOn(Notification, 'error')
  onTestFinished(() => toast.mockRestore())
  return toast
}

test('sends one request for a page 2 that fails, says so in the list and not in a toast, and loads it when the user asks', async () => {
  const toast = spyOnToasts()
  const { screen } = await renderPicker(firstPage[0].id, pageTwoFails)
  const pageTwoRequests = countRequests(pageTwoPath)

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await observersHaveRun()

  expect(pageTwoRequests()).toBe(1)
  expect(toast).not.toHaveBeenCalled()

  await commands.answerRequests([pageTwoWorks])
  await userEvent.keyboard(`${'{ArrowDown}'.repeat(firstPage.length)}{Enter}`)

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await expect.element(screen.getByRole('option', failedRow)).not.toBeInTheDocument()
  await expect.element(screen.getByRole('combobox', { name: 'Venue' })).toHaveValue(firstPage[0].name)
  expect(pageTwoRequests()).toBe(2)
})

test('a press on the failed page keeps the pick and asks again', async () => {
  const { screen, input } = await renderPicker(firstPage[0].id, pageTwoFails)
  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  const content = document.querySelector<HTMLElement>('[data-slot="combobox-content"]')!
  await Promise.all(content.getAnimations().map((animation) => animation.finished))
  const pageTwoRequests = countRequests(pageTwoPath)
  await commands.answerRequests([pageTwoWorks])

  await screen.getByRole('option', failedRow).click()

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await expect.element(input).toHaveValue(firstPage[0].name)
  await expect.element(screen.getByRole('option', { name: firstPage[0].name, exact: true })).toHaveAttribute('aria-selected', 'true')
  expect(pageTwoRequests()).toBe(1)
})

test('sends no request when the list closes and opens after a page 2 that fails', async () => {
  const { screen } = await renderPicker('', pageTwoFails)
  const toggle = screen.getByRole('button', { name: /show suggestions/i })
  await userEvent.click(toggle)
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await observersHaveRun()
  const pageOneRequests = countRequests(pageOnePath)
  const pageTwoRequests = countRequests(pageTwoPath)

  await userEvent.keyboard('{Escape}')
  await expect.element(screen.getByRole('option', failedRow)).not.toBeInTheDocument()
  await userEvent.click(toggle)
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await observersHaveRun()

  expect(pageOneRequests()).toBe(0)
  expect(pageTwoRequests()).toBe(0)
})

test('Enter on the failed page asks again, and keeps the list open and the search', async () => {
  const filters = 'name @=* "Venue"'
  const { screen, input } = await renderPicker(
    '',
    { method: 'GET', path: '/api/v1/venues', query: { filters }, headers: pagination(1, 2), body: firstPage },
    { method: 'GET', path: '/api/v1/venues', query: { filters, pageNumber: '2' }, status: 500, body: { title: 'Server error', status: 500 } },
  )

  await userEvent.fill(input, 'Venue')
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await userEvent.keyboard('{ArrowUp}')
  await expect.element(input).toHaveAttribute('aria-activedescendant', screen.getByRole('option', failedRow).element().id)

  await commands.answerRequests([
    { method: 'GET', path: '/api/v1/venues', query: { filters, pageNumber: '2' }, headers: pagination(2, 2), body: [venue(11)] },
  ])
  await userEvent.keyboard('{Enter}')

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await expect.element(input).toHaveValue('Venue')

  await userEvent.keyboard('{ArrowDown}')

  await expect.element(input).toHaveAttribute('aria-activedescendant', screen.getByRole('option', { name: 'Venue 11' }).element().id)
})

/** The answers after the fault ends, also for a pick, which reads the venue and searches for its name until it has the venue. */
const faultEnded = (): Answer[] => [
  { method: 'GET', path: '/api/v1/venues', headers: pagination(1, 2), body: firstPage },
  pageTwoWorks,
  ...[...firstPage, venue(11)].map((row): Answer => ({ method: 'GET', path: `/api/v1/venues/${row.id}`, body: row })),
]

async function failedRowIsActive(screen: Awaited<ReturnType<typeof renderPicker>>['screen'], input: Awaited<ReturnType<typeof renderPicker>>['input']) {
  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await userEvent.keyboard('{End}')
  await expect.element(input).toHaveAttribute('aria-activedescendant', screen.getByRole('option', failedRow).element().id)
}

function stopTheClock() {
  // The clock stands still, so a slow machine cannot move the second press past the end of a double press.
  vi.useFakeTimers({ toFake: ['Date'] })
  onTestFinished(() => {
    vi.useRealTimers()
  })
}

test('a second Enter just after a retry that works picks nothing and keeps the list open', async () => {
  stopTheClock()
  const { screen, input } = await renderPicker(firstPage[0].id, pageTwoFails)
  await failedRowIsActive(screen, input)
  await commands.answerRequests(faultEnded())

  await userEvent.keyboard('{Enter}')
  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await userEvent.keyboard('{Enter}')

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await expect.element(input).toHaveValue(firstPage[0].name)

  vi.advanceTimersByTime(600)
  await userEvent.keyboard('{Enter}')

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).not.toBeInTheDocument()
  await expect.element(input).not.toHaveValue(firstPage[0].name)
})

test('a second Enter after a slow retry picks nothing, also when the first press was long ago', async () => {
  stopTheClock()
  const { screen, input } = await renderPicker(firstPage[0].id, pageTwoFails)
  await failedRowIsActive(screen, input)
  await commands.answerRequests(faultEnded())
  await holdPage(pageTwoPath)

  await userEvent.keyboard('{Enter}')
  await expect.element(screen.getByRole('option', { name: 'Loading more' })).toBeVisible()
  vi.advanceTimersByTime(600)
  await commands.releaseRequests(pageTwoPath)
  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await userEvent.keyboard('{Enter}')

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await expect.element(input).toHaveValue(firstPage[0].name)
})

test('an Enter held after a retry that works picks nothing and does not submit the form', async () => {
  stopTheClock()
  const { screen, input } = await renderPicker(firstPage[0].id, pageTwoFails)
  await failedRowIsActive(screen, input)
  await commands.answerRequests(faultEnded())

  await userEvent.keyboard('{Enter}')
  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  vi.advanceTimersByTime(600)
  // A browser submits the form on each repeat that is not prevented. The answer can come after the end of one
  // userEvent.keyboard hold, and the next call releases the key, so the repeat comes from this event.
  const repeat = new KeyboardEvent('keydown', { key: 'Enter', repeat: true, bubbles: true, cancelable: true })
  input.element().dispatchEvent(repeat)

  expect(repeat.defaultPrevented).toBe(true)
  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await expect.element(input).toHaveValue(firstPage[0].name)
})

test('an arrow key after a retry that works lets the next Enter pick', async () => {
  stopTheClock()
  const { screen, input } = await renderPicker(firstPage[0].id, pageTwoFails)
  await failedRowIsActive(screen, input)
  await commands.answerRequests(faultEnded())

  await userEvent.keyboard('{Enter}')
  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await userEvent.keyboard('{ArrowDown}{Enter}')

  await expect.element(input).toHaveValue('Venue 11')
})

test('a second click just after a retry that works picks nothing and keeps the list open', async () => {
  stopTheClock()
  const { screen, input } = await renderPicker(firstPage[0].id, pageTwoFails)
  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  const content = document.querySelector<HTMLElement>('[data-slot="combobox-content"]')!
  await Promise.all(content.getAnimations().map((animation) => animation.finished))
  await commands.answerRequests(faultEnded())

  await screen.getByRole('option', failedRow).click()
  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await screen.getByRole('option', { name: 'Venue 11' }).click()

  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await expect.element(input).toHaveValue(firstPage[0].name)

  vi.advanceTimersByTime(600)
  await screen.getByRole('option', { name: 'Venue 11' }).click()

  await expect.element(input).toHaveValue('Venue 11')
})

test('a press while the retry runs picks nothing and asks for nothing more', async () => {
  const { screen, input } = await renderPicker(firstPage[0].id, pageTwoFails)
  await failedRowIsActive(screen, input)
  await commands.answerRequests(faultEnded())
  await holdPage(pageTwoPath)
  const pageTwoRequests = countRequests(pageTwoPath)

  await userEvent.keyboard('{Enter}')
  await expect.element(screen.getByRole('option', { name: 'Loading more' })).toBeVisible()
  await userEvent.keyboard('{Enter}')

  await expect.element(input).toHaveValue(firstPage[0].name)
  await expect.element(screen.getByRole('option', { name: 'Loading more' })).toBeVisible()
  expect(pageTwoRequests()).toBe(1)
})

test('a later page that fails when the list loads again shows a toast, because the list has no row to say so', async () => {
  const { screen, client } = await renderPicker()
  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', { name: 'Venue 11' })).toBeInTheDocument()
  await userEvent.keyboard('{Escape}')
  await expect.poll(() => client.isFetching()).toBe(0)
  await commands.answerRequests([{ method: 'GET', path: '/api/v1/venues', headers: pagination(1, 2), body: firstPage }, pageTwoFails])
  const toast = spyOnToasts()

  await client.invalidateQueries({ queryKey: VenueKeys.lists() })

  await expect.poll(() => toast.mock.calls.length).toBe(1)
})

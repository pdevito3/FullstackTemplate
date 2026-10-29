import { useEffect, useState } from 'react'
import { QueryClient, QueryClientProvider, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { expect, test, vi } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { leaveDeletedRecord } from './leave-deleted-record'

const detail = ['rooms', 'detail', 'r_1'] as const
const lists = ['rooms', 'list'] as const
const list = [...lists, { search: '' }] as const

// The page calls the helper in the onSuccess of mutate, so the page renders again after the mutation settles. As a
// navigation of the router, the leave takes a moment, and it ends after the next page renders.
function DetailPage({ readRoom, leave }: { readRoom: () => Promise<string>; leave: () => Promise<void> }) {
  const queryClient = useQueryClient()
  const room = useQuery({ queryKey: detail, queryFn: readRoom })
  const remove = useMutation({ mutationFn: async () => {} })
  const handleDelete = () => remove.mutate(undefined, { onSuccess: () => leaveDeletedRecord(queryClient, { detail, lists, leave }) })
  return (
    <>
      <h1>{room.data ?? 'Loading room'}</h1>
      <button type="button" onClick={handleDelete}>
        {remove.isPending ? 'Deleting' : 'Delete'}
      </button>
    </>
  )
}

function ListPage({ onShown }: { onShown: () => void }) {
  useEffect(onShown, [onShown])
  return <h1>Rooms</h1>
}

async function renderDetailPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  client.setQueryData(list, [])
  const readRoom = vi.fn(async () => 'Old Room')
  function Pages() {
    const [listShown, setListShown] = useState<{ onShown: () => void } | null>(null)
    const leave = () => new Promise<void>((resolve) => setTimeout(() => setListShown({ onShown: resolve }), 50))
    return listShown ? <ListPage onShown={listShown.onShown} /> : <DetailPage readRoom={readRoom} leave={leave} />
  }
  const screen = await render(
    <QueryClientProvider client={client}>
      <Pages />
    </QueryClientProvider>,
  )
  await expect.element(screen.getByRole('heading', { name: 'Old Room' })).toBeVisible()
  return { client, readRoom, screen }
}

test('leaves the page of the deleted record before it drops the record, so no mounted query reads it again', async () => {
  // Arrange
  const { client, readRoom, screen } = await renderDetailPage()

  // Act
  await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

  // Assert
  await expect.element(screen.getByRole('heading', { name: 'Rooms' })).toBeVisible()
  await vi.waitFor(() => expect(client.getQueryState(detail)).toBeUndefined())
  expect(readRoom).toHaveBeenCalledOnce()
})

test('marks the lists stale, so the next list reads the Server again', async () => {
  // Arrange
  const { client, screen } = await renderDetailPage()

  // Act
  await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

  // Assert
  await expect.element(screen.getByRole('heading', { name: 'Rooms' })).toBeVisible()
  expect(client.getQueryState(list)?.isInvalidated).toBe(true)
})

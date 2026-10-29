import { useState } from 'react'
import { expect, test, vi } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { SearchBox } from './search-box'

// The URL takes a search after the debounce, and a navigation can land after the user typed more.
function ListPage({ onSearch }: { onSearch: (text: string, base: string) => void }) {
  const [search, setSearch] = useState('')
  const [navigating, setNavigating] = useState<string | null>(null)
  return (
    <>
      <SearchBox
        label="Search rooms"
        search={search}
        maxLength={20}
        onSearch={(text, base) => {
          onSearch(text, base)
          setNavigating(text)
        }}
      />
      <button type="button" onClick={() => navigating !== null && setSearch(navigating)}>Finish the navigation</button>
      <button type="button" onClick={() => setSearch('Back')}>Go back</button>
    </>
  )
}

test('sends the text after the debounce with the search that it started from', async () => {
  // Arrange
  const onSearch = vi.fn()
  const screen = await render(<ListPage onSearch={onSearch} />)
  const box = screen.getByRole('searchbox', { name: 'Search rooms' })

  // Act
  await userEvent.fill(box, 'Green')

  // Assert
  await vi.waitFor(() => expect(onSearch).toHaveBeenCalledWith('Green', ''))
  expect(onSearch).toHaveBeenCalledTimes(1)
  await expect.element(box).toHaveAttribute('maxlength', '20')
})

test('keeps the text that the user typed after the debounce when its own search comes back', async () => {
  // Arrange
  const onSearch = vi.fn()
  const screen = await render(<ListPage onSearch={onSearch} />)
  const box = screen.getByRole('searchbox', { name: 'Search rooms' })
  await userEvent.fill(box, 'Gr')
  await vi.waitFor(() => expect(onSearch).toHaveBeenCalledWith('Gr', ''))
  await userEvent.type(box, 'een')

  // Act
  await userEvent.click(screen.getByRole('button', { name: 'Finish the navigation' }))

  // Assert
  await expect.element(box).toHaveValue('Green')
})

test('shows a search that comes from outside, such as Back', async () => {
  // Arrange
  const screen = await render(<ListPage onSearch={vi.fn()} />)
  const box = screen.getByRole('searchbox', { name: 'Search rooms' })
  await userEvent.fill(box, 'Typed')

  // Act
  await userEvent.click(screen.getByRole('button', { name: 'Go back' }))

  // Assert
  await expect.element(box).toHaveValue('Back')
})

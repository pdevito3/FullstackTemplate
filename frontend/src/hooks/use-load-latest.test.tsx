import { useForm } from 'react-hook-form'
import { expect, test } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { useLoadLatest } from './use-load-latest'

const FIELDS = ['name', 'capacity'] as const
interface RoomValues {
  name: string
  capacity: number
  version: number
}

function RoomForm({ latest }: { latest: RoomValues }) {
  const form = useForm<RoomValues>({ defaultValues: { name: 'Blue Room', capacity: 10, version: 1 } })
  const loadLatest = useLoadLatest(form, FIELDS)
  return (
    <form aria-label="Edit room">
      <label>Name <input {...form.register('name')} /></label>
      <label>Capacity <input type="number" {...form.register('capacity', { valueAsNumber: true })} /></label>
      <output aria-label="Version">{form.watch('version')}</output>
      <output aria-label="Changed">{loadLatest.changed?.join(', ') ?? 'none'}</output>
      <button type="button" onClick={() => loadLatest.load(async () => latest)}>Load the latest</button>
    </form>
  )
}

test('takes the saved value of each field that the latest save changed, keeps each other edit, and focuses the first change', async () => {
  // Arrange
  const screen = await render(<RoomForm latest={{ name: 'Blue Room', capacity: 30, version: 2 }} />)
  const name = screen.getByRole('textbox', { name: 'Name' })
  const capacity = screen.getByRole('spinbutton', { name: 'Capacity' })
  await userEvent.fill(name, 'Green Room')
  await userEvent.fill(capacity, '12')

  // Act
  await userEvent.click(screen.getByRole('button', { name: 'Load the latest' }))

  // Assert
  await expect.element(name).toHaveValue('Green Room')
  await expect.element(capacity).toHaveValue(30)
  await expect.element(capacity).toHaveFocus()
  await expect.element(screen.getByRole('status', { name: 'Version' })).toHaveTextContent('2')
  await expect.element(screen.getByRole('status', { name: 'Changed' })).toHaveTextContent('capacity')
})

test('names no field and focuses the first field when the latest save changed no field of the form', async () => {
  // Arrange
  const screen = await render(<RoomForm latest={{ name: 'Blue Room', capacity: 10, version: 2 }} />)
  const name = screen.getByRole('textbox', { name: 'Name' })
  await userEvent.fill(name, 'Green Room')

  // Act
  await userEvent.click(screen.getByRole('button', { name: 'Load the latest' }))

  // Assert
  await expect.element(name).toHaveValue('Green Room')
  await expect.element(name).toHaveFocus()
  await expect.element(screen.getByRole('status', { name: 'Changed' })).toHaveTextContent('')
})

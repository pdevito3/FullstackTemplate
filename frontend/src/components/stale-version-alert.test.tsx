import { expect, test, vi } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { LatestLoadedNote, StaleVersionAlert } from './stale-version-alert'

const labels = { name: 'Name', capacity: 'Capacity' }

test('says what happened to the record and what to do, and loads the latest on the button', async () => {
  // Arrange
  const onLoadLatest = vi.fn()
  const screen = await render(<StaleVersionAlert noun="room" problem={{ kind: 'stale' }} isLoadingLatest={false} onLoadLatest={onLoadLatest} />)

  // Act
  await userEvent.click(screen.getByRole('button', { name: 'Load the latest' }))

  // Assert
  await expect.element(screen.getByRole('alert').getByText('This room changed after you opened it.')).toBeVisible()
  expect(onLoadLatest).toHaveBeenCalledOnce()
})

test('shows no alert for a save that the Server refused for another reason', async () => {
  // Arrange and Act
  const screen = await render(
    <StaleVersionAlert noun="room" problem={{ kind: 'refused', messages: ['No.'] }} isLoadingLatest={false} onLoadLatest={vi.fn()} />,
  )

  // Assert
  await expect.element(screen.getByRole('alert')).not.toBeInTheDocument()
})

test('names the fields that the latest save changed, by their labels', async () => {
  // Arrange and Act
  const screen = await render(<LatestLoadedNote noun="room" changed={['name', 'capacity']} labels={labels} />)

  // Assert
  await expect.element(screen.getByRole('status')).toHaveTextContent(
    'A save after you opened this room changed Name and Capacity. The form shows the saved values of these fields. Your edits to the other fields are still in the form. Check the form, then save.',
  )
})

test('says when the latest save changed no field of the form', async () => {
  // Arrange and Act
  const screen = await render(<LatestLoadedNote noun="room" changed={[]} labels={labels} />)

  // Assert
  await expect.element(screen.getByRole('status')).toHaveTextContent(
    'The latest room has no change to the fields of this form. Your edits are still in the form. Save again to keep them.',
  )
})

import { expect, test, vi } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { FilterConfig } from '../types'
import { MultiSelectFilter } from './multiselect-filter'

const names: FilterConfig = {
  propertyKey: 'firstName',
  propertyLabel: 'First Name',
  controlType: 'multiselect',
  options: [
    { value: '"Paul"', label: 'Quoted Paul' },
    { value: 'Paul', label: 'Paul' },
    { value: 'Ann"', label: 'Ann with a quote' },
  ],
}

/** React Aria hides the native input, and its box takes the pointer, so a user clicks the label. */
async function choose(label: string): Promise<void> {
  await page.getByText(label, { exact: true }).click()
}

test('refuses an option that starts or ends with a quote and names it', async () => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<MultiSelectFilter config={names} surface="dialog" onSubmit={onSubmit} />)
  const quotedPaul = page.getByRole('checkbox', { name: 'Quoted Paul' })

  // Act
  await choose('Quoted Paul')
  await choose('Paul')

  // Assert
  const message = 'A filter value cannot start or end with a quote. Clear the option Quoted Paul.'
  await expect.element(page.getByRole('alert')).toHaveTextContent(message)
  await expect.element(quotedPaul).toHaveAccessibleDescription(message)
  await expect.element(page.getByRole('checkbox', { name: 'Paul', exact: true })).not.toHaveAccessibleDescription(message)
  await expect.element(page.getByRole('button', { name: 'Add (2)' })).toBeDisabled()
  expect(onSubmit).not.toHaveBeenCalled()
})

test('names each option with a quote at an end', async () => {
  // Arrange
  await render(<MultiSelectFilter config={names} surface="dialog" onSubmit={() => {}} />)

  // Act
  await choose('Quoted Paul')
  await choose('Ann with a quote')

  // Assert
  await expect
    .element(page.getByRole('alert'))
    .toHaveTextContent('A filter value cannot start or end with a quote. Clear the options Quoted Paul, Ann with a quote.')
})

test('adds the filter when the user clears the option with the quote', async () => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<MultiSelectFilter config={names} surface="dialog" onSubmit={onSubmit} />)
  await choose('Quoted Paul')
  await choose('Paul')

  // Act
  await choose('Quoted Paul')
  await page.getByRole('button', { name: 'Add (1)' }).click()

  // Assert
  await expect.element(page.getByRole('alert')).not.toBeInTheDocument()
  expect(onSubmit.mock.lastCall?.[0].value).toEqual(['Paul'])
})

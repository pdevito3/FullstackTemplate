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

test('adds options that start or end with a quote', async () => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<MultiSelectFilter config={names} surface="dialog" onSubmit={onSubmit} />)

  // Act
  await choose('Quoted Paul')
  await choose('Ann with a quote')
  await page.getByRole('button', { name: 'Add (2)' }).click()

  // Assert
  expect(onSubmit.mock.lastCall?.[0].value).toEqual(['"Paul"', 'Ann"'])
})

import { expect, test, vi } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { FilterConfig } from '../types'
import { TextFilter } from './text-filter'

const firstName: FilterConfig = { propertyKey: 'firstName', propertyLabel: 'First Name', controlType: 'text' }

test.each([
  { typed: '"Paul"', value: '"Paul"' },
  { typed: 'quote"', value: 'quote"' },
  { typed: '""', value: '""' },
  { typed: 'Say "hi" now', value: 'Say "hi" now' },
  { typed: '  "Paul"  ', value: '"Paul"' },
])('adds the text $typed as $value', async ({ typed, value }) => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<TextFilter config={firstName} surface="dialog" onSubmit={onSubmit} />)

  // Act
  await page.getByRole('textbox', { name: 'Value' }).fill(typed)
  await page.getByRole('button', { name: 'Add Filter' }).click()

  // Assert
  expect(onSubmit.mock.lastCall?.[0].value).toBe(value)
})

import { expect, onTestFinished, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { Filter, FilterConfig } from '../types'
import { Operators } from '../utils/operators'
import { TextFilter } from './text-filter'

const firstName: FilterConfig = { propertyKey: 'firstName', propertyLabel: 'First Name', controlType: 'text' }
const endQuoteMessage =
  'A filter value cannot start or end with a quote. Remove the quote at the start or the end.'

function filterWith(operator: Filter['operator']): Filter {
  return { id: '1', ...firstName, operator, value: 'x' }
}

test.each([
  { operator: Operators.EQUALS, value: '"Paul"' },
  { operator: Operators.EQUALS, value: '"Quoted" title' },
  { operator: Operators.STARTS_WITH, value: '"Quoted' },
  { operator: Operators.NOT_EQUALS, value: '"Paul"' },
  { operator: Operators.CONTAINS, value: 'quote"' },
  { operator: Operators.CONTAINS, value: '""' },
  { operator: Operators.CONTAINS, value: '  "Paul"  ' },
])('refuses $operator $value and tells the user why', async ({ operator, value }) => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<TextFilter config={firstName} surface="dialog" initialFilter={filterWith(operator)} onSubmit={onSubmit} />)
  const field = page.getByRole('textbox', { name: 'Value' })

  // Act
  await field.fill(value)
  await userEvent.keyboard('{Enter}')

  // Assert
  await expect.element(page.getByRole('alert')).toHaveTextContent(endQuoteMessage)
  await expect.element(field).toHaveAccessibleDescription(endQuoteMessage)
  await expect.element(field).toHaveAttribute('aria-invalid', 'true')
  await expect.element(page.getByRole('button', { name: 'Update Filter' })).toBeDisabled()
  expect(onSubmit).not.toHaveBeenCalled()
})

test('takes a quote inside the text as it is', async () => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<TextFilter config={firstName} surface="dialog" onSubmit={onSubmit} />)
  const field = page.getByRole('textbox', { name: 'Value' })

  // Act
  await field.fill('Say "hi" now')
  await page.getByRole('button', { name: 'Add Filter' }).click()

  // Assert
  expect(onSubmit.mock.lastCall?.[0].value).toBe('Say "hi" now')
  await expect.element(page.getByRole('alert')).not.toBeInTheDocument()
  await expect.element(field).not.toHaveAttribute('aria-invalid')
})

test('removes the message when the user removes the quote', async () => {
  // Arrange
  await render(<TextFilter config={firstName} surface="dialog" onSubmit={() => {}} />)
  const field = page.getByRole('textbox', { name: 'Value' })
  await field.fill('"Paul"')
  await expect.element(page.getByRole('alert')).toBeInTheDocument()

  // Act
  await field.fill('Paul')

  // Assert
  await expect.element(page.getByRole('alert')).not.toBeInTheDocument()
  await expect.element(page.getByRole('button', { name: 'Add Filter' })).toBeEnabled()
})

test('shows no alert while the user types a quote inside the text', async () => {
  // Arrange
  await render(<TextFilter config={firstName} surface="dialog" onSubmit={() => {}} />)
  const field = page.getByRole('textbox', { name: 'Value' })
  const alerts: Node[] = []
  const observer = new MutationObserver((mutations) =>
    alerts.push(...mutations.flatMap((m) => [...m.addedNodes]).filter((n) => n instanceof Element && n.closest('[role="alert"]'))),
  )
  observer.observe(document.body, { childList: true, subtree: true })
  onTestFinished(() => observer.disconnect())

  // Act
  await userEvent.type(field, 'Say "hi" now')

  // Assert
  await expect.element(field).toHaveValue('Say "hi" now')
  expect(alerts).toEqual([])
  await expect.element(page.getByRole('button', { name: 'Add Filter' })).toBeEnabled()
})

test('shows the message for a quote at the end when the field loses the focus', async () => {
  // Arrange
  await render(<TextFilter config={firstName} surface="dialog" onSubmit={() => {}} />)
  const field = page.getByRole('textbox', { name: 'Value' })
  await userEvent.type(field, 'quote"')
  await expect.element(page.getByRole('alert')).not.toBeInTheDocument()

  // Act
  await userEvent.tab()

  // Assert
  await expect.element(page.getByRole('alert')).toHaveTextContent(endQuoteMessage)
  await expect.element(field).toHaveAccessibleDescription(endQuoteMessage)
})

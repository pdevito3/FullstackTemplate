import { expect, onTestFinished, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { Filter, FilterConfig } from '../types'
import { Operators } from '../utils/operators'
import { TextFilter } from './text-filter'

const firstName: FilterConfig = { propertyKey: 'firstName', propertyLabel: 'First Name', controlType: 'text' }
const endQuoteMessage =
  'A filter value cannot start or end with a quote. Remove the quote at the start or the end.'
const bracketsMessage = 'A filter value cannot start with [ and end with ]. Remove the [ at the start or the ] at the end.'

function filterWith(operator: Filter['operator']): Filter {
  return { id: '1', ...firstName, operator, value: 'x' }
}

test.each([
  { operator: Operators.EQUALS, value: '"Paul"', message: endQuoteMessage },
  { operator: Operators.EQUALS, value: '"Quoted" title', message: endQuoteMessage },
  { operator: Operators.STARTS_WITH, value: '"Quoted', message: endQuoteMessage },
  { operator: Operators.NOT_EQUALS, value: '"Paul"', message: endQuoteMessage },
  { operator: Operators.CONTAINS, value: 'quote"', message: endQuoteMessage },
  { operator: Operators.CONTAINS, value: '""', message: endQuoteMessage },
  { operator: Operators.CONTAINS, value: '  "Paul"  ', message: endQuoteMessage },
  { operator: Operators.CONTAINS, value: '[DRAFT]', message: bracketsMessage },
  { operator: Operators.EQUALS, value: '[]', message: bracketsMessage },
])('refuses $operator $value and tells the user why', async ({ operator, value, message }) => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<TextFilter config={firstName} surface="dialog" initialFilter={filterWith(operator)} onSubmit={onSubmit} />)
  const field = page.getByRole('textbox', { name: 'Value' })

  // Act
  await field.fill(value)
  await userEvent.keyboard('{Enter}')

  // Assert
  await expect.element(page.getByRole('alert')).toHaveTextContent(message)
  await expect.element(field).toHaveAccessibleDescription(message)
  await expect.element(field).toHaveAttribute('aria-invalid', 'true')
  expect(onSubmit).not.toHaveBeenCalled()
})

test('shows the message when the user clicks Add Filter, and does not add the filter', async () => {
  // Arrange
  const onSubmit = vi.fn()
  await render(<TextFilter config={firstName} surface="dialog" onSubmit={onSubmit} />)
  const field = page.getByRole('textbox', { name: 'Value' })
  await field.fill('quote"')

  // Act
  await page.getByRole('button', { name: 'Add Filter' }).click()

  // Assert
  await expect.element(field).toHaveAccessibleDescription(endQuoteMessage)
  expect(onSubmit).not.toHaveBeenCalled()
})

test('shows the message of a filter that it edits at once', async () => {
  // Arrange
  const filter: Filter = { ...filterWith(Operators.EQUALS), value: '"John"' }

  // Act
  await render(<TextFilter config={firstName} surface="dialog" initialFilter={filter} onSubmit={() => {}} />)

  // Assert
  await expect.element(page.getByRole('textbox', { name: 'Value' })).toHaveAccessibleDescription(endQuoteMessage)
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
})

test('shows no message for a quote at the end when the field loses the focus, so that no control moves under a click', async () => {
  // Arrange
  await render(<TextFilter config={firstName} surface="dialog" onSubmit={() => {}} />)
  const field = page.getByRole('textbox', { name: 'Value' })
  await userEvent.type(field, 'quote"')

  // Act
  await userEvent.tab()

  // Assert
  await expect.element(page.getByRole('checkbox', { name: 'Case sensitive' })).toHaveFocus()
  await expect.element(page.getByRole('alert')).not.toBeInTheDocument()
})

test('takes one mouse click on Cancel after a quote at the end', async () => {
  // Arrange
  const onCancel = vi.fn()
  await render(<TextFilter config={firstName} surface="dialog" onSubmit={() => {}} onCancel={onCancel} />)
  await page.getByRole('textbox', { name: 'Value' }).fill('Paul"')

  // Act
  await page.getByRole('button', { name: 'Cancel' }).click()

  // Assert
  expect(onCancel).toHaveBeenCalledOnce()
})

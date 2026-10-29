import { expect, test } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { FilterBuilder } from './filter-builder'
import type { Filter, FilterConfig } from './types'

const firstName: FilterConfig = { propertyKey: 'firstName', propertyLabel: 'First Name', controlType: 'text' }

function firstNameEquals(id: string, value: string): Filter {
  return { id, ...firstName, operator: '==', value }
}

test.each([
  ['"John"', 'A filter value cannot start or end with a quote.'],
  ['[DRAFT]', 'A filter value cannot start with [ and end with ].'],
])('marks a filter that it gets with the value %s as not used, and tells the user why', async (value, rule) => {
  // Arrange
  const initialState = { rootLogicalOperator: 'AND' as const, filters: [firstNameEquals('1', value), firstNameEquals('2', 'Jane')] }

  // Act
  await render(<FilterBuilder filterOptions={[firstName]} initialState={initialState} />)

  // Assert
  const message = `This filter is not used. ${rule} Edit the filter, then change the value.`
  await expect.element(page.getByText(message, { exact: true })).toBeVisible()
  await expect.element(page.getByRole('button', { name: `First Name | Equals | ${value}`, exact: true })).toHaveAccessibleDescription(message)
  await expect.element(page.getByRole('button', { name: 'First Name | Equals | Jane', exact: true })).toHaveAccessibleDescription('')
})

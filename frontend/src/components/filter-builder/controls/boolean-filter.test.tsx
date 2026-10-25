import { expect, test } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import type { Filter } from '../types'
import { Operators } from '../utils/operators'
import { BooleanFilter } from './boolean-filter'

const config = { propertyKey: 'isBlocked', propertyLabel: 'Is Blocked', controlType: 'boolean' } as const

function filterOf(operator: Filter['operator'], value: boolean): Filter {
  return { id: '1', ...config, operator, value }
}

test.each([
  { operator: Operators.EQUALS, value: true, current: 'True', other: 'False' },
  { operator: Operators.NOT_EQUALS, value: true, current: 'False', other: 'True' },
])('shows $current as the current value of a $operator $value filter', async ({ operator, value, current, other }) => {
  await render(<BooleanFilter config={config} initialFilter={filterOf(operator, value)} onSubmit={() => {}} />)

  await expect.element(page.getByRole('button', { name: current })).toHaveAttribute('aria-pressed', 'true')
  await expect.element(page.getByRole('button', { name: other })).toHaveAttribute('aria-pressed', 'false')
})

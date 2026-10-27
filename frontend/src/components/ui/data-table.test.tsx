import type { ColumnDef } from '@tanstack/react-table'
import { expect, test } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { DataTable } from '@/components/ui/data-table'

interface Charge {
  id: string
  name: string
}

// The default page size is 10, so 12 charges make two pages.
const charges: Charge[] = Array.from({ length: 12 }, (_, index) => ({ id: `c${index + 1}`, name: `Charge ${index + 1}` }))
const columns: ColumnDef<Charge>[] = [{ accessorKey: 'name', header: 'Name' }]

test('the pager button keeps the focus when a keyboard press takes the table to the last or the first page', async () => {
  const screen = await render(<DataTable label="Charges" columns={columns} data={charges} />)
  const previous = screen.getByRole('button', { name: 'Previous' })
  const next = screen.getByRole('button', { name: 'Next' })

  next.element().focus()
  await userEvent.keyboard('{Enter}')

  await expect.element(screen.getByRole('rowheader', { name: 'Charge 11' })).toBeVisible()
  await expect.element(next).toHaveFocus()
  await expect.element(next).toHaveAttribute('aria-disabled', 'true')

  // A press on a button that has nothing to do does nothing.
  await userEvent.keyboard('{Enter}')
  await expect.element(screen.getByRole('rowheader', { name: 'Charge 11' })).toBeVisible()

  previous.element().focus()
  await userEvent.keyboard('{Enter}')

  await expect.element(screen.getByRole('rowheader', { name: 'Charge 1', exact: true })).toBeVisible()
  await expect.element(previous).toHaveFocus()
  await expect.element(previous).toHaveAttribute('aria-disabled', 'true')
})

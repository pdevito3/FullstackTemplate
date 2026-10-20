import { useState } from 'react'
import { Dialog, type Key } from 'react-aria-components'
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuItem,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Popover } from '@/components/ui/popover'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

// The same shape as the Add Filter menu of the filter builder: a Select in the dialog of a submenu.
function MenuWithSelect() {
  const [operator, setOperator] = useState<Key | null>('equals')
  return (
    <DropdownMenuTrigger>
      <Button>Add Filter</Button>
      <DropdownMenu>
        <DropdownMenuItem>Name</DropdownMenuItem>
        <DropdownMenuSub>
          <DropdownMenuSubTrigger>Count</DropdownMenuSubTrigger>
          <Popover placement="end top" className="w-auto p-0">
            <Dialog aria-label="Count filter" className="p-2 outline-none">
              <Select aria-label="Operator" value={operator} onChange={setOperator}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem id="equals">Equals</SelectItem>
                  <SelectItem id="greater">Greater Than</SelectItem>
                </SelectContent>
              </Select>
            </Dialog>
          </Popover>
        </DropdownMenuSub>
      </DropdownMenu>
    </DropdownMenuTrigger>
  )
}

const errors: unknown[] = []
const handleError = (event: ErrorEvent) => errors.push(event.error ?? event.message)

beforeEach(() => {
  errors.length = 0
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => errors.push(args))
  window.addEventListener('error', handleError)
})

afterEach(() => {
  window.removeEventListener('error', handleError)
  vi.restoreAllMocks()
})

test('a Select in a submenu: Escape closes the submenu before the Select opens, without a Duplicate key error', async () => {
  const screen = await render(<MenuWithSelect />)

  await screen.getByRole('button', { name: 'Add Filter' }).click()
  const menu = page.getByRole('menu', { name: 'Add Filter' })
  await expect.element(menu).toBeVisible()

  await menu.getByRole('menuitem', { name: 'Count' }).click()
  const submenu = page.getByRole('dialog', { name: 'Count filter' })
  await expect.element(submenu).toBeVisible()

  // Close the submenu before the Select ever opened. Only the submenu closes.
  await userEvent.keyboard('{Escape}')
  await expect.element(submenu).not.toBeInTheDocument()
  expect(errors).toEqual([])
  await expect.element(menu).toBeVisible()

  // The menu still works: open the submenu again and choose an operator in the Select.
  await menu.getByRole('menuitem', { name: 'Count' }).click()
  await expect.element(submenu).toBeVisible()
  await submenu.getByRole('button', { name: /Equals/ }).click()
  await page.getByRole('option', { name: 'Greater Than' }).click()
  await expect.element(submenu.getByRole('button', { name: /Greater Than/ })).toBeVisible()
  expect(errors).toEqual([])
})

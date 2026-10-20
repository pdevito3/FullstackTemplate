import { Dialog } from 'react-aria-components'
import { afterEach, expect, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import { Combobox, ComboboxContent, ComboboxInput, ComboboxItem, ComboboxList } from '@/components/ui/combobox'
import {
  DropdownMenu,
  DropdownMenuItem,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Popover } from '@/components/ui/popover'

afterEach(() => {
  vi.restoreAllMocks()
})

test('a disabled Combobox disables its input, its trigger, and its clear button', async () => {
  const screen = await render(
    <Combobox isDisabled defaultInputValue="Vue">
      <Label>Framework</Label>
      <ComboboxInput showClear />
      <ComboboxContent>
        <ComboboxList>
          <ComboboxItem id="vue">Vue</ComboboxItem>
        </ComboboxList>
      </ComboboxContent>
    </Combobox>,
  )

  await expect.element(screen.getByRole('combobox', { name: 'Framework' })).toBeDisabled()
  await expect.element(screen.getByRole('button', { name: 'Clear' })).toBeDisabled()
  // The trigger is hidden while the clear button shows, so it is not in the accessibility tree.
  expect(screen.container.querySelector('[data-slot="combobox-trigger"]')).toBeDisabled()
})

test('a Combobox in a submenu: Escape closes the submenu without a Duplicate key error', async () => {
  const errors: unknown[] = []
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => errors.push(args))
  const screen = await render(
    <DropdownMenuTrigger>
      <Button>Add Filter</Button>
      <DropdownMenu>
        <DropdownMenuItem>Name</DropdownMenuItem>
        <DropdownMenuSub>
          <DropdownMenuSubTrigger>Status</DropdownMenuSubTrigger>
          <Popover placement="end top" className="w-auto p-0">
            <Dialog aria-label="Status filter" className="p-2 outline-none">
              <Combobox aria-label="Status" defaultSelectedKey="open">
                <ComboboxInput />
                <ComboboxContent>
                  <ComboboxList>
                    <ComboboxItem id="open">Open</ComboboxItem>
                    <ComboboxItem id="closed">Closed</ComboboxItem>
                  </ComboboxList>
                </ComboboxContent>
              </Combobox>
            </Dialog>
          </Popover>
        </DropdownMenuSub>
      </DropdownMenu>
    </DropdownMenuTrigger>,
  )

  await screen.getByRole('button', { name: 'Add Filter' }).click()
  await page.getByRole('menuitem', { name: 'Status' }).click()
  const submenu = page.getByRole('dialog', { name: 'Status filter' })
  await expect.element(submenu).toBeVisible()

  await userEvent.keyboard('{Escape}')
  await expect.element(submenu).not.toBeInTheDocument()
  await expect.element(page.getByRole('menu', { name: 'Add Filter' })).toBeVisible()
  expect(errors).toEqual([])
})

import { useState } from 'react'
import { Dialog, type Key } from 'react-aria-components'
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
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

for (const theme of ['light', 'dark'] as const) {
  test(`a disabled Select has the background of a disabled Input in the ${theme} theme`, async () => {
    const screen = await render(
      <div className={theme}>
        <Input aria-label="Name" disabled />
        <Select aria-label="Status" isDisabled defaultValue="active">
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem id="active">Active</SelectItem>
          </SelectContent>
        </Select>
      </div>
    )

    const input = getComputedStyle(screen.getByRole('textbox', { name: 'Name' }).element())
    const trigger = getComputedStyle(screen.getByRole('button', { name: /Status/ }).element())
    expect(trigger.backgroundColor).toBe(input.backgroundColor)
  })
}

const longName = 'Doctor Alexandra Montgomery-Fitzgerald of the Riverside Family Practice Clinic'

test('a long Select value that does not fit the trigger ends with an ellipsis', async () => {
  const screen = await render(
    <Select aria-label="Provider" defaultValue="long" className="w-48">
      <SelectTrigger>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem id="long">{longName}</SelectItem>
      </SelectContent>
    </Select>
  )

  // Chrome draws the ellipsis of `text-overflow` only in a block container, not in a flex container.
  const value = screen.getByRole('button', { name: /Provider/ }).element().querySelector<HTMLElement>('[data-slot=select-value]')!
  const style = getComputedStyle(value)
  expect(value.scrollWidth).toBeGreaterThan(value.clientWidth)
  expect({ display: style.display, textOverflow: style.textOverflow, whiteSpace: style.whiteSpace }).toEqual({
    display: 'block',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  })
})

test('the parts of a Select value keep their size and their gap in the trigger', async () => {
  const screen = await render(
    <Select aria-label="Provider" defaultValue="alex" className="w-96">
      <SelectTrigger>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem id="alex" textValue="Alex Montgomery">
          <span data-testid="status-dot" className="size-2 rounded-full bg-green-500" />
          <span>Alex Montgomery</span>
          <span className="text-muted-foreground">alex@example.com</span>
        </SelectItem>
      </SelectContent>
    </Select>
  )

  const trigger = screen.getByRole('button', { name: /Provider/ })
  const dot = trigger.getByTestId('status-dot').element().getBoundingClientRect()
  const name = trigger.getByText('Alex Montgomery').element().getBoundingClientRect()
  const email = trigger.getByText('alex@example.com').element().getBoundingClientRect()
  expect({ width: dot.width, height: dot.height }).toEqual({ width: 8, height: 8 })
  expect([name.left - dot.right, email.left - name.right]).toEqual([8, 8])
})

test('the list of a Select as wide as the page fits in the page', async () => {
  const screen = await render(
    <Select aria-label="Operator" className="w-full">
      <SelectTrigger className="w-full">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem id="equals">Equals</SelectItem>
      </SelectContent>
    </Select>,
  )
  expect(screen.getByRole('button').element().getBoundingClientRect().width).toBe(document.documentElement.clientWidth)

  await screen.getByRole('button').click()
  await expect.element(page.getByRole('listbox')).toBeVisible()

  const content = document.querySelector<HTMLElement>('[data-slot="select-content"]')!
  await Promise.all(content.getAnimations().map((animation) => animation.finished))
  expect(content.getBoundingClientRect().left).toBeGreaterThanOrEqual(0)
  expect(content.getBoundingClientRect().right).toBeLessThanOrEqual(document.documentElement.clientWidth)
  expect(document.documentElement.scrollWidth).toBe(document.documentElement.clientWidth)
})

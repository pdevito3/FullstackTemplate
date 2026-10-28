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

test('a Combobox with only an aria-label names its list after the label', async () => {
  const screen = await render(
    <Combobox aria-label="Location">
      <ComboboxInput />
      <ComboboxContent>
        <ComboboxList>
          <ComboboxItem id="north">North</ComboboxItem>
        </ComboboxList>
      </ComboboxContent>
    </Combobox>,
  )

  await screen.getByRole('button', { name: /^Show suggestions/ }).click()

  await expect.element(page.getByRole('listbox')).toHaveAccessibleName('Suggestions Location')
})

test('a Combobox with a visible label names its list after the label', async () => {
  const screen = await render(
    <Combobox>
      <Label>Framework</Label>
      <ComboboxInput />
      <ComboboxContent>
        <ComboboxList>
          <ComboboxItem id="vue">Vue</ComboboxItem>
        </ComboboxList>
      </ComboboxContent>
    </Combobox>,
  )

  await screen.getByRole('button', { name: /^Show suggestions/ }).click()

  await expect.element(page.getByRole('listbox')).toHaveAccessibleName('Suggestions Framework')
})

test('the list of a long Combobox is at most 18rem tall on a tall page', async () => {
  await page.viewport(1280, 1400)
  const options = Array.from({ length: 60 }, (_, i) => `Option ${i + 1}`)
  const screen = await render(
    <Combobox>
      <Label>Option</Label>
      <ComboboxInput />
      <ComboboxContent>
        <ComboboxList>
          {options.map((option) => (
            <ComboboxItem key={option} id={option}>
              {option}
            </ComboboxItem>
          ))}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>,
  )

  await screen.getByRole('button', { name: /^Show suggestions/ }).click()
  await expect.element(page.getByRole('listbox')).toBeVisible()

  const content = document.querySelector<HTMLElement>('[data-slot="combobox-content"]')
  const rem = parseFloat(getComputedStyle(document.documentElement).fontSize)
  expect(content?.getBoundingClientRect().height).toBeLessThanOrEqual(18 * rem)
})

function FrameworkCombobox({ allowsCustomValue = false }: { allowsCustomValue?: boolean }) {
  return (
    <Combobox allowsCustomValue={allowsCustomValue}>
      <Label>Framework</Label>
      <ComboboxInput />
      <ComboboxContent>
        <ComboboxList>
          <ComboboxItem id="vue">Vue</ComboboxItem>
          <ComboboxItem id="vue-router">Vue Router</ComboboxItem>
          <ComboboxItem id="svelte">Svelte</ComboboxItem>
          <ComboboxItem id="solid-1">Solid</ComboboxItem>
          <ComboboxItem id="solid-2">Solid</ComboboxItem>
          <ComboboxItem id="angular" isDisabled>
            Angular
          </ComboboxItem>
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}

test('Enter picks the one option whose text is the typed text', async () => {
  const screen = await render(<FrameworkCombobox />)
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.click()
  await userEvent.keyboard('vue{Enter}')

  await expect.element(input).toHaveValue('Vue')
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
  await screen.getByRole('button', { name: /^Show suggestions/ }).click()
  await expect.element(page.getByRole('option', { name: 'Vue', exact: true })).toHaveAttribute('aria-selected', 'true')
})

test.each([
  ['no option has the text', 'Vu'],
  ['two options have the text', 'Solid'],
  ['the only option with the text is disabled', 'Angular'],
])('Enter keeps the typed text when %s', async (_, text) => {
  const screen = await render(<FrameworkCombobox />)
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.click()
  await userEvent.keyboard(`${text}{Enter}`)

  await expect.element(input).toHaveValue(text)
})

test('Enter in a Combobox that allows a custom value keeps the text and closes the list', async () => {
  const screen = await render(<FrameworkCombobox allowsCustomValue />)
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.click()
  await userEvent.keyboard('Vu{Enter}')

  await expect.element(input).toHaveValue('Vu')
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
})

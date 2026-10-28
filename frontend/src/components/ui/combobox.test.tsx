import { Dialog, type ComboBoxProps, type Key } from 'react-aria-components'
import { useState } from 'react'
import { afterEach, expect, test, vi } from 'vitest'
import { commands, page, userEvent } from 'vitest/browser'
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

function FrameworkCombobox(
  props: Pick<ComboBoxProps<object>, 'allowsCustomValue' | 'defaultSelectedKey' | 'menuTrigger' | 'onChange'>,
) {
  return (
    <Combobox {...props}>
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

test('Enter in a closed list with the text of the selected option puts back the text of the option', async () => {
  const screen = await render(<FrameworkCombobox defaultSelectedKey="vue" menuTrigger="manual" />)
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.fill('vue')
  await userEvent.keyboard('{Enter}')

  await expect.element(input).toHaveValue('Vue')
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

test('Enter that keeps the typed text in a closed list does not submit the form', async () => {
  const onSubmit = vi.fn((e: React.FormEvent) => e.preventDefault())
  const screen = await render(
    <form onSubmit={onSubmit}>
      <FrameworkCombobox defaultSelectedKey="vue" menuTrigger="manual" />
    </form>,
  )
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.fill('Vux')
  await userEvent.keyboard('{Enter}')

  await expect.element(input).toHaveValue('Vux')
  expect(onSubmit).not.toHaveBeenCalled()
})

test('Control+Enter passes to the parent and picks nothing', async () => {
  const onKeyDown = vi.fn()
  const onChange = vi.fn()
  const screen = await render(
    <div onKeyDown={(e) => onKeyDown(e.key, e.ctrlKey)}>
      <FrameworkCombobox onChange={onChange} />
    </div>,
  )
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.click()
  await userEvent.keyboard('Svelte{Control>}{Enter}{/Control}')

  expect(onKeyDown).toHaveBeenCalledWith('Enter', true)
  expect(onChange).not.toHaveBeenCalled()
})

test('the Enter that ends an IME composition in Safari picks nothing', async () => {
  const onChange = vi.fn()
  const screen = await render(<FrameworkCombobox onChange={onChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.click()
  await userEvent.keyboard('vue')
  // Safari sends this Enter with isComposing false and keyCode 229.
  input.element().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', keyCode: 229, bubbles: true, cancelable: true }))

  expect(onChange).not.toHaveBeenCalled()
})

test('Enter in a read-only Combobox picks nothing', async () => {
  const onChange = vi.fn()
  const screen = await render(
    <Combobox isReadOnly defaultInputValue="Vue" onChange={onChange}>
      <Label>Framework</Label>
      <ComboboxInput />
      <ComboboxContent>
        <ComboboxList>
          <ComboboxItem id="vue">Vue</ComboboxItem>
        </ComboboxList>
      </ComboboxContent>
    </Combobox>,
  )
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.click()
  await userEvent.keyboard('{Enter}')

  await expect.element(input).toHaveValue('Vue')
  expect(onChange).not.toHaveBeenCalled()
})

test('Enter does not select an action option that has the typed text', async () => {
  const onAction = vi.fn()
  const onChange = vi.fn()
  const screen = await render(
    <Combobox onChange={onChange}>
      <Label>Framework</Label>
      <ComboboxInput />
      <ComboboxContent>
        <ComboboxList>
          <ComboboxItem id="vue">Vue</ComboboxItem>
          <ComboboxItem id="new" onAction={onAction}>
            New
          </ComboboxItem>
        </ComboboxList>
      </ComboboxContent>
    </Combobox>,
  )
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.click()
  await userEvent.keyboard('New{Enter}')

  await expect.element(input).toHaveValue('New')
  expect(onChange).not.toHaveBeenCalled()
})

test('Enter with the text of a selected option that has a twin submits the form', async () => {
  const onSubmit = vi.fn((e: React.FormEvent) => e.preventDefault())
  const screen = await render(
    <form onSubmit={onSubmit}>
      <FrameworkCombobox defaultSelectedKey="solid-1" menuTrigger="manual" />
    </form>,
  )
  const input = screen.getByRole('combobox', { name: 'Framework' })

  await input.click()
  await userEvent.keyboard('{Enter}')

  await expect.element(input).toHaveValue('Solid')
  expect(onSubmit).toHaveBeenCalledOnce()
})

function FrameworkComboboxOnTallPage(props: Pick<ComboBoxProps<object>, 'onChange'>) {
  return (
    <div style={{ height: '300vh' }}>
      <FrameworkCombobox {...props} />
    </div>
  )
}

test('a scroll of the page closes the list and keeps the typed text, and Enter then picks the option', async () => {
  const screen = await render(<FrameworkComboboxOnTallPage />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('vue')
  await expect.element(page.getByRole('listbox')).toBeVisible()

  window.scrollBy(0, 200)

  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
  await expect.element(input).toHaveValue('vue')
  await expect.element(input).toHaveFocus()
  await userEvent.keyboard('{Enter}')
  await expect.element(input).toHaveValue('Vue')
})

test('a blur after a scroll of the page still resets text with no match', async () => {
  const onChange = vi.fn()
  const screen = await render(<FrameworkComboboxOnTallPage onChange={onChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('Vu')
  window.scrollBy(0, 200)
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()

  await userEvent.tab()

  await expect.element(input).toHaveValue('')
  expect(onChange).not.toHaveBeenCalled()
})

// React Aria makes the option under the pointer active only while the last input was a pointer. A click
// on the button opens the list, and `fill` types as a paste or dictation does, with no key press.
test.each(['{Tab}', '{Enter}'])('%s does not pick the option under the pointer', async (key) => {
  const onChange = vi.fn()
  const screen = await render(<FrameworkCombobox onChange={onChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await screen.getByRole('button', { name: /^Show suggestions/ }).click()
  await commands.movePointerOnto('option', 'Vue Router')
  await expect.element(page.getByRole('option', { name: 'Vue Router' })).toHaveAttribute('data-hovered', 'true')

  await userEvent.keyboard(key)

  await expect.element(input).toHaveValue('')
  expect(onChange).not.toHaveBeenCalled()
})

test.each([
  ['{Tab}', 'textbox', 'Next field'],
  ['{Enter}', 'combobox', 'Framework'],
] as const)('%s picks the option with the typed text, not the option under the pointer', async (key, role, focused) => {
  const onChange = vi.fn()
  const screen = await render(
    <>
      <FrameworkCombobox onChange={onChange} />
      <input aria-label="Next field" />
    </>,
  )
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await input.fill('vue')
  await commands.movePointerOnto('option', 'Vue Router')
  await expect.element(page.getByRole('option', { name: 'Vue Router' })).toHaveAttribute('data-hovered', 'true')

  await userEvent.keyboard(key)

  await expect.element(input).toHaveValue('Vue')
  expect(onChange).toHaveBeenCalledExactlyOnceWith('vue')
  await expect.element(screen.getByRole(role, { name: focused })).toHaveFocus()
})

test.each(['{Tab}', '{Enter}'])('%s picks the option that the arrow keys made active, not the option under the pointer', async (key) => {
  const onChange = vi.fn()
  const screen = await render(<FrameworkCombobox onChange={onChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await input.fill('Vu')
  await userEvent.keyboard('{ArrowDown}')
  await commands.movePointerOnto('option', 'Vue Router')
  await expect.element(page.getByRole('option', { name: 'Vue Router' })).toHaveAttribute('data-hovered', 'true')

  await userEvent.keyboard(key)

  await expect.element(input).toHaveValue('Vue')
  expect(onChange).toHaveBeenCalledExactlyOnceWith('vue')
})

function ControlledFrameworkCombobox({ onChange }: { onChange: (key: unknown) => void }) {
  const [value, setValue] = useState<Key | null>(null)
  const [inputValue, setInputValue] = useState('')
  const names: Record<string, string> = { vue: 'Vue', 'vue-router': 'Vue Router' }
  return (
    <div style={{ height: '300vh' }}>
      <Combobox
        value={value}
        onChange={(key) => {
          onChange(key)
          setValue(key)
          setInputValue(key == null ? '' : names[String(key)])
        }}
        inputValue={inputValue}
        onInputChange={setInputValue}
      >
        <Label>Framework</Label>
        <ComboboxInput />
        <ComboboxContent>
          <ComboboxList>
            <ComboboxItem id="vue">Vue</ComboboxItem>
            <ComboboxItem id="vue-router">Vue Router</ComboboxItem>
          </ComboboxList>
        </ComboboxContent>
      </Combobox>
      <input aria-label="Next field" />
    </div>
  )
}

test.each([
  ['an open list', () => {}],
  ['a list that a scroll of the page closed', () => window.scrollBy(0, 200)],
])('Tab in %s picks the option with the typed text when the value and the text are controlled', async (_, act) => {
  const onChange = vi.fn()
  const screen = await render(<ControlledFrameworkCombobox onChange={onChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('vue')
  await expect.element(page.getByRole('listbox')).toBeVisible()
  act()

  await userEvent.keyboard('{Tab}')

  await expect.element(screen.getByRole('textbox', { name: 'Next field' })).toHaveFocus()
  await expect.element(input).toHaveValue('Vue')
  expect(onChange.mock.calls).toEqual([['vue']])
})

test('a press on the button, a drag, and a release on an option picks nothing', async () => {
  const onChange = vi.fn()
  const screen = await render(<FrameworkCombobox onChange={onChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await commands.movePointerOnto('button', 'Show suggestions Framework')
  await commands.pressPointer()
  await commands.movePointerOnto('option', 'Vue Router')

  await commands.releasePointer()

  await expect.element(page.getByRole('listbox')).toBeVisible()
  await expect.element(input).toHaveValue('')
  expect(onChange).not.toHaveBeenCalled()
})

/**
 * The names of the options whose row has a fill on the screen. A moving highlight paints the active option,
 * so the test reads the pixels: the left edge of each row against the padding of the list.
 */
async function optionsWithFill() {
  const list = page.getByRole('listbox')
  const image = await createImageBitmap(
    await (await fetch(`data:image/png;base64,${await list.screenshot({ save: false })}`)).blob(),
  )
  const canvas = new OffscreenCanvas(image.width, image.height)
  const context = canvas.getContext('2d')!
  context.drawImage(image, 0, 0)
  const listBox = list.element().getBoundingClientRect()
  const scale = image.width / listBox.width
  const pixel = (x: number, y: number) =>
    context.getImageData(Math.round((x - listBox.left) * scale), Math.round((y - listBox.top) * scale), 1, 1).data.join()
  const empty = pixel(listBox.left + listBox.width / 2, listBox.top + 2)
  return [...list.element().querySelectorAll('[role=option]')]
    .filter((option) => {
      const row = option.getBoundingClientRect()
      return pixel(row.left + 3, row.top + row.height / 2) !== empty
    })
    .map((option) => option.textContent)
}

test('the option under the pointer has no fill while the arrow keys make another option active', async () => {
  // The list keeps 12 px from the edge of the page. A list wider than the page scrolls the page and closes.
  const screen = await render(
    <div style={{ padding: 12 }}>
      <FrameworkCombobox />
    </div>,
  )
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await input.fill('Vu')
  await commands.movePointerOnto('option', 'Vue Router')
  await expect.poll(optionsWithFill).toEqual(['Vue Router'])

  await userEvent.keyboard('{ArrowDown}')

  await expect.element(page.getByRole('option', { name: 'Vue Router' })).toHaveAttribute('data-hovered', 'true')
  await expect.poll(optionsWithFill).toEqual(['Vue'])
})

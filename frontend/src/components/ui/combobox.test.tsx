import { Collection, Dialog, type ComboBoxProps, type Key } from 'react-aria-components'
import { useEffect, useState } from 'react'
import { afterEach, expect, onTestFinished, test, vi, type Mock } from 'vitest'
import { commands, page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import {
  Combobox,
  ComboboxContent,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
  ComboboxLoadMore,
} from '@/components/ui/combobox'
import {
  DropdownMenu,
  DropdownMenuItem,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Popover } from '@/components/ui/popover'
import { observersHaveRun } from '@/test/observers-have-run'

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

function ControlledFrameworkCombobox({
  onChange,
  ...props
}: { onChange: (key: unknown) => void } & Pick<ComboBoxProps<object>, 'menuTrigger' | 'onBlur' | 'onOpenChange'>) {
  const [value, setValue] = useState<Key | null>(null)
  const [inputValue, setInputValue] = useState('')
  const names: Record<string, string> = { vue: 'Vue', 'vue-router': 'Vue Router' }
  return (
    <div style={{ height: '300vh' }}>
      <Combobox
        {...props}
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

test('Tab in an open list picks the option with the typed text when the value and the text are controlled', async () => {
  const onChange = vi.fn()
  const screen = await render(<ControlledFrameworkCombobox onChange={onChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('vue')
  await expect.element(page.getByRole('listbox')).toBeVisible()

  await userEvent.keyboard('{Tab}')

  await expect.element(screen.getByRole('textbox', { name: 'Next field' })).toHaveFocus()
  await expect.element(input).toHaveValue('Vue')
  expect(onChange.mock.calls).toEqual([['vue']])
})

/** Nothing signals that a list will not open, so this looks at each frame for longer than the exit of .22 s. */
async function expectListStaysClosed() {
  const end = performance.now() + 300
  while (performance.now() < end) {
    expect(page.getByRole('listbox').query()).toBeNull()
    await new Promise(requestAnimationFrame)
  }
}

const opens = (onOpenChange: Mock) => onOpenChange.mock.calls.filter(([isOpen]) => isOpen)

const closeTheList = [
  ['a scroll of the page', async () => window.scrollBy(0, 200)],
  ['the button', () => page.getByRole('button', { name: /^Show suggestions/, includeHidden: true }).click()],
] as const

const keyTimes = [
  [
    'after its exit',
    async (close: () => Promise<void>) => {
      await close()
      await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
      return () => {}
    },
  ],
  [
    'during its exit',
    // A long exit puts the key inside the exit on a slow machine too. The exit ends when the style goes.
    async (close: () => Promise<void>) => {
      const longExit = document.head.appendChild(document.createElement('style'))
      longExit.textContent = '[data-exiting] { animation-duration: 1h !important; }'
      onTestFinished(() => longExit.remove())
      await close()
      await vi.waitFor(() => expect(document.querySelector('[data-exiting]')).not.toBeNull())
      return () => longExit.remove()
    },
  ],
] as const

const closedLists = closeTheList.flatMap(([closer, close]) =>
  keyTimes.map(([keyTime, closeForKey]) => [closer, keyTime, () => closeForKey(close)] as const),
)

test.each(closedLists)('Tab picks one time in a list that %s closed, %s, with controlled value and text', async (_, __, closeForKey) => {
  const onChange = vi.fn()
  const onOpenChange = vi.fn()
  const screen = await render(<ControlledFrameworkCombobox onChange={onChange} onOpenChange={onOpenChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('vue')
  await expect.element(page.getByRole('listbox')).toBeVisible()
  const endExit = await closeForKey()
  onOpenChange.mockClear()

  await userEvent.keyboard('{Tab}')

  await expect.element(screen.getByRole('textbox', { name: 'Next field' })).toHaveFocus()
  await expect.element(input).toHaveValue('Vue')
  endExit()
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
  await expectListStaysClosed()
  expect(onChange.mock.calls).toEqual([['vue']])
  expect(opens(onOpenChange)).toEqual([])
})

test.each(closedLists)('Enter picks and keeps closed a list that %s closed, %s, with controlled value and text', async (_, __, closeForKey) => {
  const onChange = vi.fn()
  const onOpenChange = vi.fn()
  const screen = await render(<ControlledFrameworkCombobox onChange={onChange} onOpenChange={onOpenChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('vue')
  await expect.element(page.getByRole('listbox')).toBeVisible()
  const endExit = await closeForKey()
  onOpenChange.mockClear()

  await userEvent.keyboard('{Enter}')

  await expect.element(input).toHaveValue('Vue')
  endExit()
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
  await expectListStaysClosed()
  expect(onChange.mock.calls).toEqual([['vue']])
  expect(opens(onOpenChange)).toEqual([])
})

function LateTextFrameworkCombobox({
  onChange,
  onOpenChange,
  setTextLater,
}: {
  onChange: (key: unknown) => void
  onOpenChange: (isOpen: boolean) => void
  setTextLater: (setText: () => void) => void
}) {
  const [value, setValue] = useState<Key | null>(null)
  const [inputValue, setInputValue] = useState('')
  useEffect(() => {
    if (value != null) {
      setTextLater(() => setInputValue('Vue'))
    }
  }, [value, setTextLater])
  return (
    <div style={{ height: '300vh' }}>
      <Combobox
        value={value}
        onChange={(key) => {
          onChange(key)
          setValue(key)
        }}
        inputValue={inputValue}
        onInputChange={setInputValue}
        onOpenChange={onOpenChange}
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

const setTextInAnEffect = (setText: () => void) => setText()
const setTextInATimer = (setText: () => void) => void setTimeout(setText, 50)

const closeWithTheButton = () => page.getByRole('button', { name: /^Show suggestions/, includeHidden: true }).click()

const lateTextPicks = [
  ['an open list', 'an effect', async () => {}, setTextInAnEffect],
  ['an open list', 'a timer', async () => {}, setTextInATimer],
  ['a closed list', 'an effect', closeWithTheButton, setTextInAnEffect],
  ['a closed list', 'a timer', closeWithTheButton, setTextInATimer],
] as const

test.each(lateTextPicks)('Enter in %s keeps the list closed when the app sets the text in %s', async (_, __, closeOrNot, setTextLater) => {
  const onChange = vi.fn()
  const onOpenChange = vi.fn()
  const screen = await render(<LateTextFrameworkCombobox onChange={onChange} onOpenChange={onOpenChange} setTextLater={setTextLater} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('vue')
  await expect.element(page.getByRole('listbox')).toBeVisible()
  await closeOrNot()
  onOpenChange.mockClear()

  await userEvent.keyboard('{Enter}')

  await expect.element(input).toHaveValue('Vue')
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
  await expectListStaysClosed()
  expect(onChange.mock.calls).toEqual([['vue']])
  expect(opens(onOpenChange)).toEqual([])
})

test('Tab in a closed list picks one time when the app sets the text in an effect', async () => {
  const onChange = vi.fn()
  const onOpenChange = vi.fn()
  const screen = await render(<LateTextFrameworkCombobox onChange={onChange} onOpenChange={onOpenChange} setTextLater={setTextInAnEffect} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('vue')
  await expect.element(page.getByRole('listbox')).toBeVisible()
  await page.getByRole('button', { name: /^Show suggestions/, includeHidden: true }).click()
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
  onOpenChange.mockClear()

  await userEvent.keyboard('{Tab}')

  await expect.element(screen.getByRole('textbox', { name: 'Next field' })).toHaveFocus()
  await expect.element(input).toHaveValue('Vue')
  await expectListStaysClosed()
  expect(onChange.mock.calls).toEqual([['vue']])
  expect(opens(onOpenChange)).toEqual([])
})

test('the focus trigger of the app opens the list again when the focus comes back after a pick', async () => {
  const screen = await render(<ControlledFrameworkCombobox onChange={vi.fn()} menuTrigger="focus" />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await expect.element(page.getByRole('listbox')).toBeVisible()
  await userEvent.keyboard('vue')
  await page.getByRole('button', { name: /^Show suggestions/, includeHidden: true }).click()
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
  await userEvent.keyboard('{Enter}')
  await expect.element(input).toHaveValue('Vue')
  await expectListStaysClosed()
  await userEvent.keyboard('{Tab}')
  await expect.element(screen.getByRole('textbox', { name: 'Next field' })).toHaveFocus()

  await userEvent.keyboard('{Shift>}{Tab}{/Shift}')

  await expect.element(input).toHaveFocus()
  await expect.element(page.getByRole('listbox')).toBeVisible()
})

test('a blur of the input calls the onBlur of the app', async () => {
  const onBlur = vi.fn()
  const screen = await render(<ControlledFrameworkCombobox onChange={vi.fn()} onBlur={onBlur} />)
  await screen.getByRole('combobox', { name: 'Framework' }).click()

  await userEvent.keyboard('{Tab}')

  await expect.element(screen.getByRole('textbox', { name: 'Next field' })).toHaveFocus()
  expect(onBlur).toHaveBeenCalledOnce()
})

test('typing after Enter in a closed list opens the list when the value and the text are controlled', async () => {
  const screen = await render(<ControlledFrameworkCombobox onChange={vi.fn()} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await userEvent.keyboard('vue')
  await expect.element(page.getByRole('listbox')).toBeVisible()
  await page.getByRole('button', { name: /^Show suggestions/, includeHidden: true }).click()
  await expect.element(page.getByRole('listbox')).not.toBeInTheDocument()
  await userEvent.keyboard('{Enter}')
  await expect.element(input).toHaveValue('Vue')

  await userEvent.keyboard('{Backspace}')

  await expect.element(page.getByRole('listbox')).toBeVisible()
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

test('the list of a Combobox as wide as the page fits in the page, and ArrowDown and Enter pick an option', async () => {
  const onChange = vi.fn()
  const screen = await render(<FrameworkCombobox onChange={onChange} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  expect(input.element().closest('[data-slot="input-group"]')?.getBoundingClientRect().width).toBe(
    document.documentElement.clientWidth,
  )

  await screen.getByRole('button', { name: /^Show suggestions/ }).click()
  await expect.element(page.getByRole('listbox')).toBeVisible()
  const content = document.querySelector<HTMLElement>('[data-slot="combobox-content"]')!
  await Promise.all(content.getAnimations().map((animation) => animation.finished))
  const list = content.getBoundingClientRect()
  expect(list.left).toBeGreaterThanOrEqual(0)
  expect(list.right).toBeLessThanOrEqual(document.documentElement.clientWidth)
  await userEvent.keyboard('{ArrowDown}')
  await userEvent.keyboard('{Enter}')

  expect(window.scrollX).toBe(0)
  await expect.element(input).toHaveValue('Vue')
  expect(onChange).toHaveBeenCalledWith('vue')
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
  const screen = await render(<FrameworkCombobox />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await input.click()
  await input.fill('Vu')
  await commands.movePointerOnto('option', 'Vue Router')
  await expect.poll(optionsWithFill).toEqual(['Vue Router'])

  await userEvent.keyboard('{ArrowDown}')

  await expect.element(page.getByRole('option', { name: 'Vue Router' })).toHaveAttribute('data-hovered', 'true')
  await expect.poll(optionsWithFill).toEqual(['Vue'])
})

function LoadMoreList({ finished, onLoadMore }: { finished: number; onLoadMore: () => void }) {
  const [asked, setAsked] = useState(0)
  return (
    <Combobox aria-label="Framework">
      <ComboboxInput />
      <ComboboxContent>
        <ComboboxList>
          <Collection items={[{ id: 'vue' }]}>{({ id }) => <ComboboxItem id={id}>Vue</ComboboxItem>}</Collection>
          <ComboboxLoadMore
            isLoading={asked > finished}
            isFailed={false}
            failedText="More frameworks did not load."
            onLoadMore={() => {
              onLoadMore()
              setAsked((count) => count + 1)
            }}
          />
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}

test('ComboboxLoadMore asks for more one time while isLoading is true, and asks again after it', async () => {
  const onLoadMore = vi.fn()
  const screen = await render(<LoadMoreList finished={0} onLoadMore={onLoadMore} />)

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByText('Loading more')).toBeVisible()
  await observersHaveRun()

  expect(onLoadMore).toHaveBeenCalledOnce()

  await screen.rerender(<LoadMoreList finished={1} onLoadMore={onLoadMore} />)

  await expect.poll(() => onLoadMore.mock.calls.length).toBe(2)
})

function FailedLoadMoreList({
  isLoading,
  onLoadMore,
  onChange,
  failedText = 'More frameworks did not load.',
}: {
  isLoading: boolean
  onLoadMore: () => void
  onChange?: (key: Key | null) => void
  failedText?: string
}) {
  return (
    // The items prop turns off the filter of React Aria, as in a picker that searches on the Server.
    <Combobox aria-label="Framework" items={[{ id: 'vue' }]} onChange={onChange}>
      <ComboboxInput />
      <ComboboxContent>
        <ComboboxList>
          <Collection items={[{ id: 'vue' }]}>{({ id }) => <ComboboxItem id={id}>Vue</ComboboxItem>}</Collection>
          <ComboboxLoadMore isLoading={isLoading} isFailed failedText={failedText} onLoadMore={onLoadMore} />
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}

const failedRow = { name: 'More frameworks did not load. Try again' }

test('ComboboxLoadMore asks for nothing after a failure, and asks once for each Enter on its option', async () => {
  const onLoadMore = vi.fn()
  const onChange = vi.fn()
  const screen = await render(<FailedLoadMoreList isLoading={false} onLoadMore={onLoadMore} onChange={onChange} />)

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await observersHaveRun()
  expect(onLoadMore).not.toHaveBeenCalled()

  await userEvent.keyboard('{ArrowUp}{Enter}')

  expect(onLoadMore).toHaveBeenCalledOnce()
  expect(onChange).not.toHaveBeenCalled()
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
})

test('ComboboxLoadMore asks for nothing on Enter on its option while the page loads again', async () => {
  const onLoadMore = vi.fn()
  const screen = await render(<FailedLoadMoreList isLoading onLoadMore={onLoadMore} />)

  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', { name: 'Loading more' })).toBeVisible()
  await userEvent.keyboard('{ArrowUp}{Enter}')

  await expect.element(screen.getByRole('option', { name: 'Loading more' })).toBeVisible()
  expect(onLoadMore).not.toHaveBeenCalled()
})

test('ComboboxLoadMore tells a screen reader that the page failed and where to try again', async () => {
  await render(<FailedLoadMoreList isLoading={false} onLoadMore={vi.fn()} />)

  await expect
    .poll(() => document.querySelector('[data-live-announcer]')?.textContent)
    .toContain('More frameworks did not load. Go to the end of the list to try again.')
})

test('a press on the option of ComboboxLoadMore asks once and keeps the list open and the typed text', async () => {
  const onLoadMore = vi.fn()
  const screen = await render(<FailedLoadMoreList isLoading={false} onLoadMore={onLoadMore} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await userEvent.fill(input, 'Vue')
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  const content = document.querySelector<HTMLElement>('[data-slot="combobox-content"]')!
  await Promise.all(content.getAnimations().map((animation) => animation.finished))

  await screen.getByRole('option', failedRow).click()

  expect(onLoadMore).toHaveBeenCalledOnce()
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await expect.element(input).toHaveValue('Vue')
})

function everyReadOfTheClockIsLater() {
  let now = Date.now()
  const clock = vi.spyOn(Date, 'now').mockImplementation(() => (now += 1000))
  onTestFinished(() => clock.mockRestore())
}

function stopTheClock() {
  // The clock stands still, so a slow machine cannot move a second press past the end of a double press.
  vi.useFakeTimers({ toFake: ['Date'] })
  onTestFinished(() => {
    vi.useRealTimers()
  })
}

async function renderFailedListInForm() {
  const onSubmit = vi.fn((e: React.FormEvent) => e.preventDefault())
  const onLoadMore = vi.fn()
  const screen = await render(
    <form onSubmit={onSubmit}>
      <FailedLoadMoreList isLoading={false} onLoadMore={onLoadMore} />
      <button type="submit">Save</button>
    </form>,
  )
  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await userEvent.keyboard('{ArrowUp}')
  await expect.element(screen.getByRole('combobox', { name: 'Framework' })).toHaveAttribute('aria-activedescendant', screen.getByRole('option', failedRow).element().id)
  return { screen, onSubmit, onLoadMore }
}

test('Enter on the option of ComboboxLoadMore does not submit the form', async () => {
  const { onSubmit, onLoadMore } = await renderFailedListInForm()

  await userEvent.keyboard('{Enter}')

  expect(onLoadMore).toHaveBeenCalledOnce()
  expect(onSubmit).not.toHaveBeenCalled()
})

test('a held Enter on the option of ComboboxLoadMore asks once and does not submit the form', async () => {
  const { screen, onSubmit, onLoadMore } = await renderFailedListInForm()
  everyReadOfTheClockIsLater()

  await userEvent.keyboard('{Enter>5/}')

  expect(onLoadMore).toHaveBeenCalledOnce()
  expect(onSubmit).not.toHaveBeenCalled()
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
})

test('a second Enter on the option of ComboboxLoadMore asks only after the time of a double press', async () => {
  stopTheClock()
  const { onLoadMore } = await renderFailedListInForm()

  await userEvent.keyboard('{Enter}{Enter}')
  expect(onLoadMore).toHaveBeenCalledOnce()

  vi.advanceTimersByTime(600)
  await userEvent.keyboard('{Enter}')
  expect(onLoadMore).toHaveBeenCalledTimes(2)
})

test('Enter submits the form just after a retry when the list has closed', async () => {
  stopTheClock()
  const { screen, onSubmit, onLoadMore } = await renderFailedListInForm()
  await userEvent.keyboard('{Enter}')
  expect(onLoadMore).toHaveBeenCalledOnce()

  window.dispatchEvent(new Event('scroll'))
  await expect.element(screen.getByRole('option', failedRow)).not.toBeInTheDocument()
  await userEvent.keyboard('{Enter}')

  expect(onLoadMore).toHaveBeenCalledOnce()
  expect(onSubmit).toHaveBeenCalledOnce()
})

test('only Enter on the option of ComboboxLoadMore asks for the page', async () => {
  const onLoadMore = vi.fn()
  const screen = await render(<FailedLoadMoreList isLoading={false} onLoadMore={onLoadMore} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await userEvent.keyboard('{ArrowUp}')
  await expect.element(input).toHaveAttribute('aria-activedescendant', screen.getByRole('option', failedRow).element().id)

  await userEvent.keyboard('{ArrowUp}')

  await expect.element(input).toHaveAttribute('aria-activedescendant', screen.getByRole('option', { name: 'Vue' }).element().id)
  expect(onLoadMore).not.toHaveBeenCalled()
})

test('ComboboxLoadMore tells a screen reader only when the page has failed, and not while it loads again', async () => {
  const failedText = 'More plugins did not load.'
  const announced = () => document.querySelector('[data-live-announcer]')?.textContent ?? ''
  const screen = await render(<FailedLoadMoreList isLoading onLoadMore={vi.fn()} failedText={failedText} />)
  await observersHaveRun()

  expect(announced()).not.toContain(failedText)

  await screen.rerender(<FailedLoadMoreList isLoading={false} onLoadMore={vi.fn()} failedText={failedText} />)

  await expect.poll(announced).toContain(failedText)
})

test('the option of ComboboxLoadMore stays the active option while the page loads again, with no failure color', async () => {
  const screen = await render(<FailedLoadMoreList isLoading={false} onLoadMore={vi.fn()} />)
  const input = screen.getByRole('combobox', { name: 'Framework' })
  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()
  await userEvent.keyboard('{ArrowUp}')
  await expect.element(screen.getByRole('option', failedRow)).toHaveClass('text-destructive')

  await screen.rerender(<FailedLoadMoreList isLoading onLoadMore={vi.fn()} />)

  const loading = screen.getByRole('option', { name: 'Loading more' })
  await expect.element(loading).toBeVisible()
  await expect.element(input).toHaveAttribute('aria-activedescendant', loading.element().id)
  await expect.element(loading).not.toHaveClass('text-destructive')
})

test('the option of ComboboxLoadMore adds no space to the list', async () => {
  const screen = await render(<FailedLoadMoreList isLoading={false} onLoadMore={vi.fn()} />)
  await userEvent.click(screen.getByRole('button', { name: /show suggestions/i }))
  await expect.element(screen.getByRole('option', failedRow)).toBeVisible()

  const list = screen.getByRole('listbox').element()

  expect(list.scrollHeight).toBe(list.clientHeight)
})

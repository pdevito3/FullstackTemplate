import * as React from 'react'
import { expect, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import {
  Command,
  CommandDialog,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import { Dialog, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Drawer, DrawerClose, DrawerFooter, DrawerHeader, DrawerTitle, DrawerTrigger } from '@/components/ui/drawer'
import { Input } from '@/components/ui/input'

function InlineCommand({ onAction }: { onAction?: () => void }) {
  return (
    <Command>
      <CommandInput placeholder="Search" />
      <CommandList aria-label="Commands">
        <CommandGroup heading="Suggestions">
          <CommandItem onAction={onAction}>Calendar</CommandItem>
          <CommandItem>Calculator</CommandItem>
        </CommandGroup>
      </CommandList>
    </Command>
  )
}

const searchBox = () => page.getByRole('searchbox', { name: 'Search' })
const calendar = () => page.getByRole('menuitem', { name: 'Calendar' })
const isHighlighted = () => calendar().element().hasAttribute('data-focused')

async function waitForTheHoverToSettle() {
  await expect.poll(() => calendar().element().matches(':hover')).toBe(true)
  await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))
}

test('a hover on an item leaves the focus on a control outside the Command, with no blur', async () => {
  // Arrange
  const onBlur = vi.fn()
  await render(
    <>
      <Input aria-label="Email" onBlur={onBlur} />
      <InlineCommand />
    </>
  )
  const email = page.getByRole('textbox', { name: 'Email' })
  await email.click()

  // Act
  await calendar().hover()
  await waitForTheHoverToSettle()

  // Assert
  await expect.element(email).toHaveFocus()
  expect(onBlur).not.toHaveBeenCalled()
  await userEvent.keyboard('c')
  await expect.element(email).toHaveValue('c')
  await expect.element(searchBox()).toHaveValue('')
})

test('a hover on an item highlights it while the focus is in the input of the Command', async () => {
  // Arrange
  await render(<InlineCommand />)
  await searchBox().click()

  // Act
  await calendar().hover()
  await waitForTheHoverToSettle()

  // Assert
  await expect.element(searchBox()).toHaveFocus()
  expect(isHighlighted()).toBe(true)
})

test('a hover on an item shows a highlight while the focus is outside the Command', async () => {
  // Arrange
  await render(
    <>
      <Input aria-label="Email" />
      <InlineCommand />
    </>
  )
  await page.getByRole('textbox', { name: 'Email' }).click()
  const background = () => getComputedStyle(calendar().element()).backgroundColor
  const backgroundWithNoHover = background()

  // Act
  await calendar().hover()
  await waitForTheHoverToSettle()

  // Assert
  expect(isHighlighted()).toBe(false)
  expect(background()).not.toBe(backgroundWithNoHover)
})

test('a click on an item with the focus outside the Command runs the item and moves the focus to the input', async () => {
  // Arrange
  const onAction = vi.fn()
  await render(
    <>
      <Input aria-label="Email" />
      <InlineCommand onAction={onAction} />
    </>
  )
  await page.getByRole('textbox', { name: 'Email' }).click()

  // Act
  await calendar().click()

  // Assert
  expect(onAction).toHaveBeenCalledOnce()
  await expect.element(searchBox()).toHaveFocus()
})

test('a hover on an item of a command dialog moves the focus back to the input after a click on a heading', async () => {
  // Arrange
  await render(
    <CommandDialog isOpen onOpenChange={() => {}}>
      <InlineCommand />
    </CommandDialog>
  )
  const dialog = page.getByRole('dialog', { name: 'Command Palette' })
  await expect.element(dialog).toBeInTheDocument()
  await page.getByText('Suggestions').click()
  await expect.element(dialog).toHaveFocus()

  // Act
  await calendar().hover()
  await waitForTheHoverToSettle()

  // Assert
  await expect.element(searchBox()).toHaveFocus()
})

// The Command fills the window under the overlay, so that the pointer rests on its item when the overlay closes.
function CommandUnderTheOverlay({ children }: { children: React.ReactNode }) {
  return (
    <>
      <div className="relative z-10">{children}</div>
      <div className="fixed inset-0">
        <Command>
          <CommandInput placeholder="Search" />
          <CommandList aria-label="Commands" className="max-h-none">
            <CommandItem className="h-[200vh]">Calendar</CommandItem>
          </CommandList>
        </Command>
      </div>
    </>
  )
}

function ControlledDrawer() {
  const [isOpen, setOpen] = React.useState(false)
  return (
    <>
      <Button onPress={() => setOpen(true)}>Open</Button>
      <Drawer isOpen={isOpen} onOpenChange={setOpen}>
        <DrawerHeader>
          <DrawerTitle>Overlay</DrawerTitle>
        </DrawerHeader>
        <DrawerFooter>
          <DrawerClose>Done</DrawerClose>
        </DrawerFooter>
      </Drawer>
    </>
  )
}

function TriggerDrawer() {
  return (
    <DrawerTrigger>
      <Button>Open</Button>
      <Drawer>
        <DrawerHeader>
          <DrawerTitle>Overlay</DrawerTitle>
        </DrawerHeader>
        <DrawerFooter>
          <DrawerClose>Done</DrawerClose>
        </DrawerFooter>
      </Drawer>
    </DrawerTrigger>
  )
}

function TriggerDialog() {
  return (
    <DialogTrigger>
      <Button>Open</Button>
      <Dialog showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Overlay</DialogTitle>
        </DialogHeader>
        <Button slot="close">Done</Button>
      </Dialog>
    </DialogTrigger>
  )
}

const overlays = [
  { name: 'a controlled drawer', Overlay: ControlledDrawer },
  { name: 'a drawer in a trigger', Overlay: TriggerDrawer },
  { name: 'a dialog', Overlay: TriggerDialog },
]

test.each(overlays)('a mouse click on Done closes $name over a Command item, and the focus goes to the trigger', async ({ Overlay }) => {
  // Arrange
  await render(<CommandUnderTheOverlay><Overlay /></CommandUnderTheOverlay>)
  const trigger = page.getByRole('button', { name: 'Open' })
  await trigger.click()
  const overlay = page.getByRole('dialog', { name: 'Overlay' })
  await expect.element(overlay).toHaveFocus()
  await expect.poll(() => document.querySelector('[data-entering]')).toBeNull()

  // Act
  await page.getByRole('button', { name: 'Done' }).click()

  // Assert
  await expect.element(overlay).not.toBeInTheDocument()
  await waitForTheHoverToSettle()
  await expect.element(trigger).toHaveFocus()
})

test.each([
  { name: 'the keyboard on Done', close: async () => {
    page.getByRole('button', { name: 'Done' }).element().focus()
    await userEvent.keyboard('{Enter}')
  } },
  { name: 'Escape', close: () => userEvent.keyboard('{Escape}') },
])('$name closes a controlled drawer over a Command item, and the focus goes to the trigger', async ({ close }) => {
  // Arrange
  await render(<CommandUnderTheOverlay><ControlledDrawer /></CommandUnderTheOverlay>)
  const trigger = page.getByRole('button', { name: 'Open' })
  await trigger.click()
  const overlay = page.getByRole('dialog', { name: 'Overlay' })
  await expect.element(overlay).toHaveFocus()
  await expect.poll(() => document.querySelector('[data-entering]')).toBeNull()

  // Act
  await close()

  // Assert
  await expect.element(overlay).not.toBeInTheDocument()
  await expect.element(trigger).toHaveFocus()
})

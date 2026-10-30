import { expect, test } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import { Dialog, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import {
  Drawer,
  DrawerClose,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from '@/components/ui/drawer'

function DrawerExample({ preventDismissal }: { preventDismissal?: boolean }) {
  return (
    <DrawerTrigger>
      <Button variant="outline">Open Drawer</Button>
      <Drawer preventDismissal={preventDismissal}>
        <DrawerHeader>
          <DrawerTitle>Drawer Title</DrawerTitle>
          <DrawerDescription>Press Escape to close the drawer.</DrawerDescription>
        </DrawerHeader>
        <DrawerFooter>
          <DrawerClose>Done</DrawerClose>
        </DrawerFooter>
      </Drawer>
    </DrawerTrigger>
  )
}

const overlay = () => document.querySelector('[data-slot="drawer-overlay"]')

test('Escape closes the drawer while it slides in, and the focus goes back to the trigger', async () => {
  // Arrange
  await render(<DrawerExample />)
  const trigger = page.getByRole('button', { name: 'Open Drawer' })
  await trigger.click()
  await expect.element(page.getByRole('dialog', { name: 'Drawer Title' })).toBeInTheDocument()
  expect(overlay()?.hasAttribute('data-entering')).toBe(true)

  // Act
  await userEvent.keyboard('{Escape}')

  // Assert
  await expect.element(page.getByRole('dialog')).not.toBeInTheDocument()
  await expect.element(trigger).toHaveFocus()
})

test('Escape closes the drawer after it slides in, and the focus goes back to the trigger', async () => {
  // Arrange
  await render(<DrawerExample />)
  const trigger = page.getByRole('button', { name: 'Open Drawer' })
  await trigger.click()
  await expect.element(page.getByRole('dialog', { name: 'Drawer Title' })).toHaveFocus()

  // Act
  await userEvent.keyboard('{Escape}')

  // Assert
  await expect.element(page.getByRole('dialog')).not.toBeInTheDocument()
  await expect.element(trigger).toHaveFocus()
})

test('Escape does not close a drawer that prevents dismissal, also while it slides in', async () => {
  // Arrange
  await render(<DrawerExample preventDismissal />)
  await page.getByRole('button', { name: 'Open Drawer' }).click()
  await expect.element(page.getByRole('dialog', { name: 'Drawer Title' })).toBeInTheDocument()
  expect(overlay()?.hasAttribute('data-entering')).toBe(true)

  // Act
  await userEvent.keyboard('{Escape}')
  await expect.element(page.getByRole('dialog', { name: 'Drawer Title' })).toHaveFocus()
  await userEvent.keyboard('{Escape}')

  // Assert
  await expect.element(page.getByRole('dialog', { name: 'Drawer Title' })).toBeInTheDocument()
  expect(overlay()?.hasAttribute('data-exiting')).toBe(false)
})

test('Escape while a drawer slides in over a dialog closes only the drawer', async () => {
  // Arrange
  await render(
    <DialogTrigger>
      <Button>Open Dialog</Button>
      <Dialog>
        <DialogTitle>Dialog Title</DialogTitle>
        <DrawerExample />
      </Dialog>
    </DialogTrigger>
  )
  await page.getByRole('button', { name: 'Open Dialog' }).click()
  await expect.element(page.getByRole('dialog', { name: 'Dialog Title' })).toHaveFocus()
  const trigger = page.getByRole('button', { name: 'Open Drawer' })
  await trigger.click()
  await expect.element(page.getByRole('dialog', { name: 'Drawer Title' })).toBeInTheDocument()
  expect(overlay()?.hasAttribute('data-entering')).toBe(true)

  // Act
  await userEvent.keyboard('{Escape}')

  // Assert
  await expect.element(page.getByRole('dialog', { name: 'Drawer Title' })).not.toBeInTheDocument()
  await expect.element(trigger).toHaveFocus()
  expect(document.querySelector('[data-exiting]')).toBeNull()
  await expect.element(page.getByRole('dialog', { name: 'Dialog Title' })).toBeInTheDocument()
})

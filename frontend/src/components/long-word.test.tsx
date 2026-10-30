import type { ReactNode } from 'react'
import { Label } from 'react-aria-components'
import { afterEach, expect, test } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Notification, toastQueue } from '@/components/notification'
import { PageHeading } from '@/components/page-heading'
import { QueryError } from '@/components/query-error'
import { RefusedSaveAlert } from '@/components/refused-save-alert'
import { Toaster } from '@/components/toaster'
import { Card, CardAction, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Combobox, ComboboxContent, ComboboxInput, ComboboxItem, ComboboxList } from '@/components/ui/combobox'
import { Dialog, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Drawer, DrawerDescription, DrawerHeader, DrawerTitle } from '@/components/ui/drawer'
import { FieldDescription, FieldError } from '@/components/ui/field'
import { MultiSelect } from '@/components/ui/multi-select'
import {
  ResponsiveDialog,
  ResponsiveDialogDescription,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
} from '@/components/ui/responsive-dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Sheet, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'

const longWord = 'Northdepotfleetmaintenanceworkorderforthevehiclewithplatexyz'

afterEach(() => {
  toastQueue.clear()
})

function textOverflow(text: Element, box: Element): string | undefined {
  const range = document.createRange()
  range.selectNodeContents(text)
  const { left, right } = range.getBoundingClientRect()
  const boxRect = box.getBoundingClientRect()
  if (right > boxRect.right + 0.5 || left < boxRect.left - 0.5) {
    return `the text spans ${Math.round(left)} to ${Math.round(right)}, its box ${Math.round(boxRect.left)} to ${Math.round(boxRect.right)}`
  }
  if (right > window.innerWidth + 0.5) return `the text ends at ${Math.round(right)}, past the window`
  const lines = [...range.getClientRects()]
  const { fontSize, lineHeight } = getComputedStyle(text)
  if (new Set(lines.map((line) => Math.round(line.top))).size > 1 && parseFloat(lineHeight) < 1.2 * parseFloat(fontSize)) {
    return `the text wraps with a line height of ${lineHeight} for a font size of ${fontSize}`
  }
  for (const close of box.querySelectorAll('[data-slot$="-close"]')) {
    const button = close.getBoundingClientRect()
    const covered = lines.some(
      (line) => line.left < button.right && line.right > button.left && line.top < button.bottom && line.bottom > button.top,
    )
    if (covered) return 'the close button covers the text'
  }
  return undefined
}

async function openAnimationsEnded() {
  // The modal starts its animations after it paints.
  await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))
  await Promise.allSettled(document.getAnimations().map((animation) => animation.finished))
}

async function expectLongWordToFit(textSelector: string, boxSelector: string, count: number) {
  await expect.poll(() => document.querySelectorAll(textSelector).length).toBe(count)
  await openAnimationsEnded()
  for (const text of document.querySelectorAll(textSelector)) {
    const box = text.closest(boxSelector)
    expect(box, `${boxSelector} holds ${textSelector}`).not.toBeNull()
    expect(textOverflow(text, box!), `${text.getAttribute('data-slot') ?? text.tagName}`).toBeUndefined()
  }
}

const cases: { name: string; ui: () => ReactNode; open?: () => Promise<void>; text: string; box: string; count: number }[] = [
  {
    name: 'responsive dialog',
    ui: () => (
      <ResponsiveDialog isOpen>
        <ResponsiveDialogHeader>
          <ResponsiveDialogTitle>{longWord} details</ResponsiveDialogTitle>
          <ResponsiveDialogDescription>{longWord} goes away.</ResponsiveDialogDescription>
        </ResponsiveDialogHeader>
      </ResponsiveDialog>
    ),
    text: '[data-slot=responsive-dialog-title], [data-slot=responsive-dialog-description]',
    box: '[data-slot=responsive-dialog-content]',
    count: 2,
  },
  {
    name: 'dialog',
    ui: () => (
      <Dialog isOpen>
        <DialogHeader>
          <DialogTitle>{longWord} details</DialogTitle>
          <DialogDescription>{longWord} goes away.</DialogDescription>
        </DialogHeader>
      </Dialog>
    ),
    text: '[data-slot=dialog-title], [data-slot=dialog-description]',
    box: '[data-slot=dialog-content]',
    count: 2,
  },
  {
    name: 'sheet',
    ui: () => (
      <Sheet isOpen>
        <SheetHeader>
          <SheetTitle>{longWord} details</SheetTitle>
          <SheetDescription>{longWord} goes away.</SheetDescription>
        </SheetHeader>
      </Sheet>
    ),
    text: '[data-slot=sheet-title], [data-slot=sheet-description]',
    box: '[data-slot=sheet-content]',
    count: 2,
  },
  {
    name: 'drawer',
    ui: () => (
      <Drawer isOpen>
        <DrawerHeader>
          <DrawerTitle>{longWord} details</DrawerTitle>
          <DrawerDescription>{longWord} goes away.</DrawerDescription>
        </DrawerHeader>
      </Drawer>
    ),
    text: '[data-slot=drawer-title], [data-slot=drawer-description]',
    box: '[data-slot=drawer-content]',
    count: 2,
  },
  {
    name: 'card',
    ui: () => (
      <Card>
        <CardHeader>
          <CardTitle>{longWord}</CardTitle>
          <CardDescription>{longWord}</CardDescription>
          <CardAction>Edit</CardAction>
        </CardHeader>
      </Card>
    ),
    text: '[data-slot=card-title], [data-slot=card-description]',
    box: '[data-slot=card]',
    count: 2,
  },
  {
    name: 'page heading',
    ui: () => <PageHeading>{longWord}</PageHeading>,
    text: 'h1',
    box: 'body',
    count: 1,
  },
  {
    name: 'query error',
    ui: () => <QueryError title={`Could not load ${longWord}`} error={new Error(`${longWord} was not found.`)} onRetry={() => {}} />,
    text: '[role=alert] p',
    box: '[role=alert]',
    count: 2,
  },
  {
    name: 'refused save alert',
    ui: () => <RefusedSaveAlert problem={{ kind: 'refused', messages: [`${longWord} is in use.`] }} />,
    text: '[role=alert] p',
    box: '[role=alert]',
    count: 1,
  },
  {
    name: 'select option',
    ui: () => (
      <Select defaultOpen aria-label="Venue">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem id="venue">{longWord}</SelectItem>
        </SelectContent>
      </Select>
    ),
    text: '[data-slot=select-item] > span:first-child',
    box: '[role=listbox]',
    count: 1,
  },
  {
    name: 'combobox option',
    ui: () => (
      <Combobox>
        <Label>Venue</Label>
        <ComboboxInput />
        <ComboboxContent>
          <ComboboxList>
            <ComboboxItem id="venue">{longWord}</ComboboxItem>
          </ComboboxList>
        </ComboboxContent>
      </Combobox>
    ),
    open: async () => {
      await userEvent.click(page.getByRole('combobox', { name: 'Venue' }))
      await userEvent.keyboard('{ArrowDown}')
    },
    text: '[data-slot=combobox-item]',
    box: '[role=listbox]',
    count: 1,
  },
  {
    name: 'multi-select chip',
    ui: () => <MultiSelect options={[{ value: 'venue', label: longWord }]} defaultValue={['venue']} />,
    text: '[data-slot=combobox-chip]',
    box: '[data-slot=combobox-chips]',
    count: 1,
  },
  {
    name: 'field description and error',
    ui: () => (
      <div data-testid="field">
        <FieldDescription>Sent to {longWord}@example.com</FieldDescription>
        <FieldError>{longWord}@example.com is in use.</FieldError>
      </div>
    ),
    text: '[data-slot=field-description], [data-slot=field-error]',
    box: '[data-testid=field]',
    count: 2,
  },
]

for (const width of [320, 1280]) {
  for (const { name, ui, open, text, box, count } of cases) {
    test(`at ${width} px, a ${name} wraps a long word in its box`, async () => {
      // Arrange
      await page.viewport(width, 800)

      // Act
      await render(ui())
      await open?.()

      // Assert
      await expectLongWordToFit(text, box, count)
    })
  }

  test(`at ${width} px, a toast wraps a long word in its title and description`, async () => {
    // Arrange
    await page.viewport(width, 800)
    await render(<Toaster />)

    // Act
    Notification.success(`Deleted ${longWord}`, { description: `${longWord} goes away.` })

    // Assert
    await expectLongWordToFit('[data-slot=toast] [slot=title], [data-slot=toast] [slot=description]', '[data-slot=toast]', 2)
  })
}

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
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
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

// Cards are usually in a grid. At 1280 px this grid puts three cards in a row.
const cardGrid = 'grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3'

// A page form is 512 px wide at 1280 px and the long word fits in it. A form in a dialog is narrower.
function DialogForm({ children }: { children: ReactNode }) {
  return (
    <ResponsiveDialog isOpen>
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>Edit the place</ResponsiveDialogTitle>
      </ResponsiveDialogHeader>
      <form className="flex flex-col gap-5">{children}</form>
    </ResponsiveDialog>
  )
}

afterEach(() => {
  toastQueue.clear()
})

// The line boxes of the text nodes only. A range over the element also holds the boxes of its child elements, such as a button.
// The rectangle of a text is the content area of the font, and a font can be taller than its line height.
// The line box is the line height around the content area. Chrome rounds the top half of the leading down.
function textLines(text: Element): DOMRect[] {
  const walker = document.createTreeWalker(text, NodeFilter.SHOW_TEXT)
  const range = document.createRange()
  const lines: DOMRect[] = []
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    range.selectNodeContents(node)
    const lineHeight = parseFloat(getComputedStyle(node.parentElement!).lineHeight)
    for (const { left, top, width, height } of range.getClientRects()) {
      const lineBox = Number.isNaN(lineHeight) ? height : lineHeight
      lines.push(new DOMRect(left, top - Math.floor((lineBox - height) / 2), width, lineBox))
    }
  }
  return lines
}

function textOverflow(text: Element, box: Element): string | undefined {
  const lines = textLines(text)
  if (lines.length === 0) return 'the text shows no line'
  const left = Math.min(...lines.map((line) => line.left))
  const right = Math.max(...lines.map((line) => line.right))
  const top = Math.min(...lines.map((line) => line.top))
  const bottom = Math.max(...lines.map((line) => line.bottom))
  const boxRect = box.getBoundingClientRect()
  if (right > boxRect.right + 0.5 || left < boxRect.left - 0.5) {
    return `the text spans ${Math.round(left)} to ${Math.round(right)}, its box ${Math.round(boxRect.left)} to ${Math.round(boxRect.right)}`
  }
  if (bottom > boxRect.bottom + 0.5 || top < boxRect.top - 0.5) {
    return `the text spans ${Math.round(top)} to ${Math.round(bottom)} down, its box ${Math.round(boxRect.top)} to ${Math.round(boxRect.bottom)}`
  }
  if (right > window.innerWidth + 0.5) return `the text ends at ${Math.round(right)}, past the window`
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

async function expectLongWordToFit(textSelector: string, boxSelectors: string | string[], count: number) {
  await expect.poll(() => document.querySelectorAll(textSelector).length).toBe(count)
  await openAnimationsEnded()
  for (const text of document.querySelectorAll(textSelector)) {
    for (const boxSelector of [boxSelectors].flat()) {
      const box = text.closest(boxSelector)
      expect(box, `${boxSelector} holds ${textSelector}`).not.toBeNull()
      expect(textOverflow(text, box!), `${text.getAttribute('data-slot') ?? text.tagName} in ${boxSelector}`).toBeUndefined()
    }
  }
}

// A part gets a 1280 px case only when its real layout at 1280 px is too narrow for the long word.
// In a wider layout the word fits, and the case cannot fail.
const allWidths = [320, 1280]
const phoneOnly = [320]

const cases: {
  name: string
  ui: () => ReactNode
  open?: () => Promise<void>
  text: string
  box: string | string[]
  count: number
  widths: number[]
}[] = [
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
    widths: allWidths,
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
    widths: allWidths,
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
    widths: allWidths,
  },
  {
    name: 'drawer',
    ui: () => (
      <Drawer isOpen>
        {/* A bottom drawer is as wide as the window, so its content is in a narrow column. */}
        <div data-testid="drawer-column" className="mx-auto w-full max-w-sm">
          <DrawerHeader>
            <DrawerTitle>{longWord} details</DrawerTitle>
            <DrawerDescription>{longWord} goes away.</DrawerDescription>
          </DrawerHeader>
        </div>
      </Drawer>
    ),
    text: '[data-slot=drawer-title], [data-slot=drawer-description]',
    box: '[data-testid=drawer-column]',
    count: 2,
    widths: allWidths,
  },
  {
    name: 'card',
    ui: () => (
      <div className={cardGrid}>
        <Card>
          <CardHeader>
            <CardTitle>{longWord}</CardTitle>
            <CardDescription>{longWord}</CardDescription>
            <CardAction>Edit</CardAction>
          </CardHeader>
        </Card>
      </div>
    ),
    text: '[data-slot=card-title], [data-slot=card-description]',
    box: '[data-slot=card]',
    count: 2,
    widths: allWidths,
  },
  {
    name: 'page heading',
    ui: () => <PageHeading>{longWord}</PageHeading>,
    text: 'h1',
    box: 'body',
    count: 1,
    widths: phoneOnly,
  },
  {
    name: 'query error',
    ui: () => <QueryError title={`Could not load ${longWord}`} error={new Error(`${longWord} was not found.`)} onRetry={() => {}} />,
    text: '[role=alert] p',
    box: '[role=alert]',
    count: 2,
    widths: phoneOnly,
  },
  {
    name: 'refused save alert',
    ui: () => (
      <DialogForm>
        <RefusedSaveAlert problem={{ kind: 'refused', messages: [`${longWord} is in use.`] }} />
      </DialogForm>
    ),
    text: '[role=alert] p',
    box: '[role=alert]',
    count: 1,
    widths: allWidths,
  },
  {
    name: 'select option',
    ui: () => (
      <Select defaultOpen aria-label="Place">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem id="place">{longWord}</SelectItem>
        </SelectContent>
      </Select>
    ),
    text: '[data-slot=select-item] > span:first-child',
    box: '[role=listbox]',
    count: 1,
    widths: allWidths,
  },
  {
    name: 'combobox option',
    ui: () => (
      <DialogForm>
        <Combobox>
          <Label>Place</Label>
          <ComboboxInput />
          <ComboboxContent>
            <ComboboxList>
              <ComboboxItem id="place">{longWord}</ComboboxItem>
            </ComboboxList>
          </ComboboxContent>
        </Combobox>
      </DialogForm>
    ),
    open: async () => {
      await userEvent.click(page.getByRole('combobox', { name: 'Place' }))
      await userEvent.keyboard('{ArrowDown}')
    },
    text: '[data-slot=combobox-item]',
    box: '[role=listbox]',
    count: 1,
    widths: allWidths,
  },
  {
    name: 'multi-select chip',
    ui: () => (
      <DialogForm>
        <MultiSelect options={[{ value: 'place', label: longWord }]} defaultValue={['place']} />
      </DialogForm>
    ),
    text: '[data-slot=combobox-chip]',
    box: ['[data-slot=combobox-chip]', '[data-slot=combobox-chips]'],
    count: 1,
    widths: phoneOnly,
  },
  {
    name: 'field description and error',
    ui: () => (
      <DialogForm>
        <Field>
          <FieldLabel htmlFor="email">Email</FieldLabel>
          <Input id="email" />
          <FieldDescription>Sent to {longWord}@example.com</FieldDescription>
          <FieldError>{longWord}@example.com is in use.</FieldError>
        </Field>
      </DialogForm>
    ),
    text: '[data-slot=field-description], [data-slot=field-error]',
    box: '[data-slot=field]',
    count: 2,
    widths: allWidths,
  },
]

for (const { name, ui, open, text, box, count, widths } of cases) {
  for (const width of widths) {
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
}

// A toast is at most 500 px wide at each width, so a phone is the only width where the long word does not fit.
test('at 320 px, a toast wraps a long word in its title and description', async () => {
  // Arrange
  await page.viewport(320, 800)
  await render(<Toaster />)

  // Act
  Notification.success(`Deleted ${longWord}`, { description: `${longWord} goes away.` })

  // Assert
  await expectLongWordToFit('[data-slot=toast] [slot=title], [data-slot=toast] [slot=description]', '[data-slot=toast]', 2)
})

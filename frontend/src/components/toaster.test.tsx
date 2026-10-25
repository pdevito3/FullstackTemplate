import { useState, type ReactNode } from 'react'
import { afterEach, beforeEach, expect, test, vi, type MockInstance } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Notification, toastQueue } from '@/components/notification'
import { Toaster } from '@/components/toaster'
import { Drawer, DrawerClose, DrawerFooter, DrawerHeader, DrawerTitle } from '@/components/ui/drawer'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
} from '@/components/ui/responsive-dialog'

let startViewTransition: MockInstance<Document['startViewTransition']>

beforeEach(() => {
  startViewTransition = vi.spyOn(document, 'startViewTransition')
})

afterEach(() => {
  vi.restoreAllMocks()
  toastQueue.clear()
})

// While a view transition runs, the browser hit-tests its snapshot, not the page. A panel that still slides in moves
// after the test measures it.
async function animationsFinished(): Promise<void> {
  await Promise.all(startViewTransition.mock.results.map(({ value }) => value.finished))
  await Promise.all(document.getAnimations().map((animation) => animation.finished))
}

function boxOf(text: string): DOMRect {
  return page.getByRole('alertdialog').filter({ hasText: text }).element().getBoundingClientRect()
}

// userEvent.click passes even when the toast region covers the point, so the tests read the hit test of the browser.
function clickTargetAt(x: number, y: number, target: Element): 'target' | 'toast region' | undefined {
  const region = document.querySelector('[data-slot="toast-region"]')
  const hit = document.elementsFromPoint(x, y).find((element) => target.contains(element) || region?.contains(element))
  if (!hit) return undefined
  return target.contains(hit) ? 'target' : 'toast region'
}

test('a click beside a toast or between two toasts reaches the page under the toast region', async () => {
  // From 501 to 767 px the region spans the screen, and each toast is 500 px wide and centered.
  await page.viewport(767, 1024)
  await render(
    <>
      <button type="button" className="fixed inset-0">
        Page
      </button>
      <Toaster />
    </>,
  )

  Notification.info('First')
  Notification.info('Second')
  await expect.element(page.getByText('Second')).toBeVisible()
  await animationsFinished()

  const pageButton = page.getByRole('button', { name: 'Page' }).element()
  const [upper, lower] = [boxOf('First'), boxOf('Second')].sort((a, b) => a.top - b.top)
  expect(clickTargetAt(lower.left - 40, lower.top + lower.height / 2, pageButton), 'beside the toast').toBe('target')
  expect(clickTargetAt(lower.left + lower.width / 2, (upper.bottom + lower.top) / 2, pageButton), 'between the toasts').toBe(
    'target',
  )
})

interface PanelProps {
  isOpen: boolean
  onOpenChange: (isOpen: boolean) => void
}

interface OpenPanelProps {
  panel: (props: PanelProps) => ReactNode
}

function OpenPanel({ panel }: OpenPanelProps) {
  const [isOpen, setIsOpen] = useState(true)
  return panel({ isOpen, onOpenChange: setIsOpen })
}

const bottomPanels = {
  'bottom sheet': (props: PanelProps) => (
    <ResponsiveDialog {...props}>
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>Edit filter</ResponsiveDialogTitle>
      </ResponsiveDialogHeader>
      <ResponsiveDialogFooter>
        <ResponsiveDialogClose>Continue</ResponsiveDialogClose>
      </ResponsiveDialogFooter>
    </ResponsiveDialog>
  ),
  drawer: (props: PanelProps) => (
    <Drawer {...props}>
      <DrawerHeader>
        <DrawerTitle>Edit filter</DrawerTitle>
      </DrawerHeader>
      <DrawerFooter>
        <DrawerClose>Continue</DrawerClose>
      </DrawerFooter>
    </Drawer>
  ),
}

test.each(Object.entries(bottomPanels))(
  'on a phone, the toasts show at the top while a %s is open, and its button takes the click',
  async (_, panel) => {
    await page.viewport(390, 844)
    await render(
      <>
        <OpenPanel panel={panel} />
        <Toaster />
      </>,
    )
    const dialog = page.getByRole('dialog', { name: 'Edit filter' })
    await expect.element(dialog).toBeVisible()

    Notification.error('Could not save the changes')
    await expect.element(page.getByText('Could not save the changes')).toBeVisible()
    await animationsFinished()

    const continueButton = dialog.getByRole('button', { name: 'Continue' })
    const { left, top, width, height } = continueButton.element().getBoundingClientRect()
    expect(clickTargetAt(left + width / 2, top + height / 2, continueButton.element())).toBe('target')
    expect(boxOf('Could not save the changes').top).toBe(16)
    await userEvent.click(continueButton)
    await expect.element(dialog).not.toBeInTheDocument()
    expect(boxOf('Could not save the changes').bottom).toBe(window.innerHeight - 16)
  },
)

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

afterEach(async () => {
  vi.useRealTimers()
  toastQueue.clear()
  // The clear plays a view transition. It must end here, or the next test waits for it and gets an AbortError.
  await animationsFinished()
  vi.restoreAllMocks()
})

// While a view transition runs, the browser hit-tests its snapshot, not the page. A panel that still slides in moves
// after the test measures it.
async function animationsFinished(): Promise<void> {
  await Promise.all(startViewTransition.mock.results.map(({ value }) => value.finished))
  await Promise.all(document.getAnimations().map((animation) => animation.finished))
}

// A toast pauses or restarts its timer in a ResizeObserver callback. The browser runs all the callbacks of one rendering
// step in one task, so the test goes on after all of them.
async function resizeObserverCallbacks(): Promise<void> {
  await new Promise<void>((resolve) => {
    const observer = new ResizeObserver(() => {
      observer.disconnect()
      resolve()
    })
    observer.observe(document.body)
  })
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

test('on a phone on its side, the toast at the top does not cover a tall bottom sheet', async () => {
  await page.viewport(667, 375)
  await render(
    <>
      <OpenPanel
        panel={(props) => (
          <ResponsiveDialog {...props}>
            <ResponsiveDialogHeader>
              <ResponsiveDialogTitle>Edit filter</ResponsiveDialogTitle>
            </ResponsiveDialogHeader>
            <div className="h-[600px]" />
          </ResponsiveDialog>
        )}
      />
      <Toaster />
    </>,
  )
  const dialog = page.getByRole('dialog', { name: 'Edit filter' })
  await expect.element(dialog).toBeVisible()

  Notification.error('Could not save the changes')
  await expect.element(page.getByText('Could not save the changes')).toBeVisible()
  await animationsFinished()

  expect(boxOf('Could not save the changes').bottom).toBeLessThanOrEqual(dialog.element().getBoundingClientRect().top)
})

const tallBottomPanels = {
  'bottom sheet': (props: PanelProps) => (
    <ResponsiveDialog {...props}>
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>Edit filter</ResponsiveDialogTitle>
      </ResponsiveDialogHeader>
      <div className="h-[600px]" />
    </ResponsiveDialog>
  ),
  drawer: (props: PanelProps) => (
    <Drawer {...props}>
      <DrawerHeader>
        <DrawerTitle>Edit filter</DrawerTitle>
      </DrawerHeader>
      <div className="h-[600px] shrink-0" />
    </Drawer>
  ),
}

test.each(Object.entries(tallBottomPanels))(
  'on a phone on its side, a toast of three lines at the top does not cover a tall %s',
  async (_, panel) => {
    await page.viewport(667, 375)
    await render(
      <>
        <OpenPanel panel={panel} />
        <Toaster />
      </>,
    )
    const dialog = page.getByRole('dialog', { name: 'Edit filter' })
    await expect.element(dialog).toBeVisible()

    Notification.error('Could not save the changes', {
      description: 'The server did not answer in time. Check the connection, and then try again in a few minutes.',
    })
    await expect.element(page.getByText('Could not save the changes')).toBeVisible()
    await animationsFinished()

    const panelTopWithDragHandle = dialog.element().parentElement!.getBoundingClientRect().top
    expect(boxOf('Could not save the changes').height).toBeGreaterThan(80)
    expect(boxOf('Could not save the changes').bottom).toBeLessThanOrEqual(panelTopWithDragHandle)
  },
)

test('on a phone on its side, a toast that came while the pointer was on the toasts keeps waiting when a newer toast hides it', async () => {
  await page.viewport(667, 375)
  await render(
    <>
      <OpenPanel panel={bottomPanels['bottom sheet']} />
      <Toaster />
    </>,
  )
  await expect.element(page.getByRole('dialog', { name: 'Edit filter' })).toBeVisible()
  // Only the test moves the clock of the toast timers, so a slow browser does not use up their time.
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'Date'] })
  Notification.info('Upload started', { duration: 20_000 })
  await expect.element(page.getByText('Upload started')).toBeVisible()
  await animationsFinished()

  await userEvent.hover(page.getByText('Upload started'))
  Notification.error('Could not save the changes', { duration: 10_000 })
  await expect.element(page.getByText('Could not save the changes')).toBeVisible()
  await userEvent.hover(page.getByRole('heading', { name: 'Edit filter' }))
  Notification.info('Upload finished', { duration: 20_000 })
  await expect.element(page.getByText('Upload finished')).toBeVisible()
  await animationsFinished()
  await resizeObserverCallbacks()
  vi.advanceTimersByTime(10_500)
  await animationsFinished()

  expect(page.getByRole('alertdialog').filter({ hasText: 'Could not save the changes' }).query()).not.toBeNull()
})

function CenteredDialog({ contentHeight }: { contentHeight: number }) {
  return (
    <ResponsiveDialog isOpen className="md:max-w-[400px]">
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>Add filter</ResponsiveDialogTitle>
      </ResponsiveDialogHeader>
      <div style={{ height: contentHeight }} />
      <ResponsiveDialogFooter>
        <ResponsiveDialogClose>Continue</ResponsiveDialogClose>
      </ResponsiveDialogFooter>
    </ResponsiveDialog>
  )
}

const twoToastsWithDescriptions = () => {
  Notification.error('Something went wrong', { description: 'The server did not answer. Try again.' })
  Notification.success('Changes saved', { description: 'The filter list is up to date.' })
}

// The modal starts its zoom animation after it paints.
async function openAnimationsFinished(): Promise<void> {
  await expect.element(page.getByRole('dialog', { name: 'Add filter' })).toBeVisible()
  await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))
  await animationsFinished()
}

function centeredDialogBox(): DOMRect {
  return page.getByRole('dialog', { name: 'Add filter' }).element().getBoundingClientRect()
}

test('on a desktop, a centered dialog stays in the middle while no toast shows', async () => {
  await page.viewport(900, 800)
  await render(
    <>
      <CenteredDialog contentHeight={440} />
      <Toaster />
    </>,
  )
  await openAnimationsFinished()

  const { top, bottom } = centeredDialogBox()
  expect(top).toBeCloseTo(window.innerHeight - bottom, 0)
})

test('on a desktop, two toasts do not cover the button of a wide centered dialog', async () => {
  await page.viewport(900, 800)
  await render(
    <>
      <CenteredDialog contentHeight={440} />
      <Toaster />
    </>,
  )
  const dialog = page.getByRole('dialog', { name: 'Add filter' })
  await openAnimationsFinished()

  twoToastsWithDescriptions()
  await expect.element(page.getByText('Changes saved')).toBeVisible()
  await animationsFinished()

  const continueButton = dialog.getByRole('button', { name: 'Continue' }).element()
  const { left, right, top, height } = continueButton.getBoundingClientRect()
  for (const x of [left + 1, right - 1]) expect(clickTargetAt(x, top + height / 2, continueButton)).toBe('target')
  const upperToast = Math.min(boxOf('Something went wrong').top, boxOf('Changes saved').top)
  expect(upperToast - centeredDialogBox().bottom).toBe(8)
})

test('on a desktop, a dialog taller than the room above two toasts gets shorter and keeps its button in view', async () => {
  await page.viewport(900, 800)
  await render(
    <>
      <CenteredDialog contentHeight={1200} />
      <Toaster />
    </>,
  )
  const dialog = page.getByRole('dialog', { name: 'Add filter' })
  await openAnimationsFinished()

  twoToastsWithDescriptions()
  await expect.element(page.getByText('Changes saved')).toBeVisible()
  await animationsFinished()

  const continueButton = dialog.getByRole('button', { name: 'Continue' }).element()
  const { left, right, top, height } = continueButton.getBoundingClientRect()
  for (const x of [left + 1, right - 1]) expect(clickTargetAt(x, top + height / 2, continueButton)).toBe('target')
  expect(centeredDialogBox().top).toBe(32)
  expect(Math.min(boxOf('Something went wrong').top, boxOf('Changes saved').top) - centeredDialogBox().bottom).toBe(8)
})

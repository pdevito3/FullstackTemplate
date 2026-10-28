import { afterEach, expect, test } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
} from '@/components/ui/responsive-dialog'

const longTitle = 'Close work order WO-2026-00042 for the north depot vehicle'
const longDescription =
  'Closing this work order records the final odometer reading, marks every open task as done, and sends a notice to ' +
  'the fleet manager. You cannot reopen a closed work order later, so check each field first.'

afterEach(async () => {
  await page.viewport(414, 896)
})

function LongDialog({ title, description }: { title: string; description?: string }) {
  return (
    <ResponsiveDialog isOpen>
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>{title}</ResponsiveDialogTitle>
        {description && <ResponsiveDialogDescription>{description}</ResponsiveDialogDescription>}
      </ResponsiveDialogHeader>
      {Array.from({ length: 20 }, (_, index) => (
        <p key={index} className="py-3">
          Field {index + 1}
        </p>
      ))}
      <ResponsiveDialogFooter>
        <ResponsiveDialogClose>Close work order</ResponsiveDialogClose>
      </ResponsiveDialogFooter>
    </ResponsiveDialog>
  )
}

function FormDialog({ title, description }: { title: string; description?: string }) {
  return (
    <ResponsiveDialog isOpen>
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>{title}</ResponsiveDialogTitle>
        {description && <ResponsiveDialogDescription>{description}</ResponsiveDialogDescription>}
      </ResponsiveDialogHeader>
      {Array.from({ length: 20 }, (_, index) => (
        <input key={index} aria-label={`Field ${index + 1}`} className="my-2 block h-8 w-full border" />
      ))}
      <ResponsiveDialogFooter>
        <ResponsiveDialogClose>Cancel</ResponsiveDialogClose>
        <ResponsiveDialogClose>Save</ResponsiveDialogClose>
      </ResponsiveDialogFooter>
    </ResponsiveDialog>
  )
}

function isHidden(element: Element): boolean {
  const { left, right, top, bottom } = element.getBoundingClientRect()
  return [
    [left + 1, top + 1],
    [right - 1, bottom - 1],
  ].some(([x, y]) => !element.contains(document.elementFromPoint(x, y)))
}

function closeButton(): Element {
  return page.getByRole('button', { name: 'Close', exact: true }).element()
}

function scrollParent(element: Element): Element {
  let parent = element.parentElement
  while (parent && getComputedStyle(parent).overflowY !== 'auto') parent = parent.parentElement
  if (!parent) throw new Error('The content has no scroll container')
  return parent
}

async function openAnimationsFinished(): Promise<void> {
  await expect.element(page.getByRole('dialog')).toBeVisible()
  // The modal starts its animations after it paints.
  await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))
  // The line above the dialog footer follows the scroll of the dialog, so its animation finishes only at the end.
  const animations = document.getAnimations().filter(
    (animation) => !(animation instanceof CSSAnimation && animation.animationName === 'dialog-footer-edge'),
  )
  await Promise.all(animations.map((animation) => animation.finished))
}

function paragraphTextUnderCloseButton(x: number, y: number): string | undefined {
  const button = closeButton()
  const shown = document.elementsFromPoint(x, y).find((element) => !button.contains(element))
  return shown?.closest('p')?.textContent ?? undefined
}

for (const [width, height] of [
  [1280, 720],
  [390, 844],
]) {
  test(`at ${width}x${height} the title of a long dialog does not run under the close button`, async () => {
    await page.viewport(width, height)
    render(<LongDialog title={longTitle} description={longDescription} />)
    await openAnimationsFinished()

    const button = closeButton().getBoundingClientRect()
    const range = document.createRange()
    range.selectNodeContents(page.getByRole('heading', { name: longTitle }).element())
    const linesUnderButton = Array.from(range.getClientRects()).filter(
      (line) => line.left < button.right && button.left < line.right && line.top < button.bottom && button.top < line.bottom,
    )
    expect(linesUnderButton).toEqual([])
  })

  test(`at ${width}x${height} the content of a long dialog does not scroll under the close button`, async () => {
    await page.viewport(width, height)
    render(<LongDialog title="Edit filter" />)
    await openAnimationsFinished()

    scrollParent(page.getByText('Field 1', { exact: true }).element()).scrollTop = 120

    await expect.poll(() => {
      const { left, right, top, bottom } = closeButton().getBoundingClientRect()
      expect(closeButton().contains(document.elementFromPoint((left + right) / 2, (top + bottom) / 2))).toBe(true)
      return [
        [left + 1, top + 1],
        [right - 1, top + 1],
        [left + 1, bottom - 1],
        [right - 1, bottom - 1],
      ].map(([x, y]) => paragraphTextUnderCloseButton(x, y)).filter((text) => text?.startsWith('Field '))
    }).toEqual([])
  })

  test(`at ${width}x${height} the header and footer of a long form do not hide the field in focus`, async () => {
    await page.viewport(width, height)
    render(<FormDialog title={longTitle} description={longDescription} />)
    await openAnimationsFinished()

    scrollParent(page.getByRole('textbox', { name: 'Field 1', exact: true }).element()).scrollTop = 40
    const focused: string[] = []
    const hidden: string[] = []
    async function press(keys: string) {
      await userEvent.keyboard(keys)
      const field = document.activeElement!
      focused.push(field.ariaLabel!)
      if (isHidden(field)) hidden.push(field.ariaLabel!)
    }
    for (let index = 1; index <= 20; index++) await press('{Tab}')
    for (let index = 19; index >= 1; index--) await press('{Shift>}{Tab}{/Shift}')

    expect(focused[0]).toBe('Field 1')
    expect(hidden).toEqual([])
  })

  test(`at ${width}x${height} a field in focus that scrolls under the header does not show around the title`, async () => {
    await page.viewport(width, height)
    render(<FormDialog title="Edit filter" />)
    await openAnimationsFinished()

    const field = page.getByRole('textbox', { name: 'Field 4', exact: true }).element() as HTMLElement
    field.focus()
    const box = scrollParent(field)
    const title = page.getByRole('heading', { name: 'Edit filter' }).element()
    box.scrollTop += field.getBoundingClientRect().top - title.getBoundingClientRect().top
    expect(field.getBoundingClientRect().top).toBeCloseTo(title.getBoundingClientRect().top, 0)

    // A focus ring is 3 px wide, and the text of the title reaches about 3 px below its box.
    await expect.poll(() => {
      const header = title.closest('[data-slot="responsive-dialog-header"]')!
      const { left } = box.getBoundingClientRect()
      const right = left + box.clientWidth
      const { top, bottom } = title.getBoundingClientRect()
      const shown = new Set<string>()
      for (let y = top; y <= bottom + 3; y++) {
        for (let x = left + 1; x < right; x += 2) {
          const hit = document.elementFromPoint(x, y)
          if (!hit || header.contains(hit) || closeButton().contains(hit)) continue
          shown.add(hit === field ? 'the field' : hit.getAttribute('class') ?? hit.tagName)
        }
      }
      return [...shown]
    }).toEqual([])
  })

  test(`at ${width}x${height} the footer of a long dialog stays in view`, async () => {
    await page.viewport(width, height)
    render(<LongDialog title={longTitle} description={longDescription} />)

    await expect.poll(() => {
      const action = page.getByRole('button', { name: 'Close work order' }).element()
      const { left, top, width: w, height: h } = action.getBoundingClientRect()
      return action.contains(document.elementFromPoint(left + w / 2, top + h / 2))
    }).toBe(true)
  })
}

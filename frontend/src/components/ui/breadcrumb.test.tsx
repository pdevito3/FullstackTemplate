import { createMemoryHistory, createRootRoute, createRouter, RouterProvider } from '@tanstack/react-router'
import { expect, test } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage } from '@/components/ui/breadcrumb'

test.each([320, 375])('a long trail stays on one line in a %i px header and truncates only the current page', async (width) => {
  const trail = () => (
    <header style={{ width }} className="flex h-16 items-center gap-2">
      <div className="flex min-w-0 items-center gap-2 px-4">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink to="/">Work orders</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbItem>
              <BreadcrumbPage>T-101 work order for the north yard compressor</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
      </div>
    </header>
  )
  const router = createRouter({ routeTree: createRootRoute({ component: trail }), history: createMemoryHistory() })
  const screen = await render(<RouterProvider router={router} />)
  await expect.element(screen.getByRole('link', { name: 'Work orders' })).toBeVisible()

  const header = screen.container.querySelector('header')!
  const crumb = screen.getByRole('link', { name: 'Work orders' }).element()
  const crumbItem = crumb.closest('li')!
  const current = screen.container.querySelector<HTMLElement>('[data-slot="breadcrumb-page"]')!
  const name = [current, ...current.querySelectorAll('*')].find((el) => getComputedStyle(el).textOverflow === 'ellipsis')!
  const lineHeight = parseFloat(getComputedStyle(name).lineHeight)
  const middle = (el: Element) => (el.getBoundingClientRect().top + el.getBoundingClientRect().bottom) / 2

  expect(name.getBoundingClientRect().height).toBeLessThanOrEqual(lineHeight)
  expect(crumb.getBoundingClientRect().height).toBeLessThanOrEqual(lineHeight)
  expect(Math.abs(middle(name) - middle(crumb))).toBeLessThanOrEqual(1)
  expect(crumbItem.scrollWidth).toBeLessThanOrEqual(crumbItem.clientWidth)
  expect(name.scrollWidth).toBeGreaterThan(name.clientWidth)
  expect(current.getBoundingClientRect().right).toBeLessThanOrEqual(header.getBoundingClientRect().right)
})

test('a BreadcrumbLink as the last item is the current page: disabled, not an anchor, and not focusable', async () => {
  const trail = () => (
    <Breadcrumb>
      <BreadcrumbList>
        <BreadcrumbItem>
          <BreadcrumbLink to="/">Work orders</BreadcrumbLink>
        </BreadcrumbItem>
      </BreadcrumbList>
    </Breadcrumb>
  )
  const router = createRouter({ routeTree: createRootRoute({ component: trail }), history: createMemoryHistory() })
  const screen = await render(<RouterProvider router={router} />)

  const current = screen.getByRole('link', { name: 'Work orders' })
  await expect.element(current).toHaveAttribute('aria-disabled', 'true')
  await expect.element(current).toHaveAttribute('aria-current', 'page')
  await expect.element(current).not.toHaveAttribute('tabindex')
  expect(current.element().closest('a')).toBeNull()
})

const workOrder = 'T-101 work order for the north yard compressor'

async function renderWorkOrderTrail(width: number) {
  const trail = () => (
    <header style={{ width }} className="flex h-16 items-center gap-2">
      <div className="flex min-w-0 items-center gap-2 px-4">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink to="/">Work orders</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbItem>
              <BreadcrumbPage>{workOrder}</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
      </div>
    </header>
  )
  const router = createRouter({ routeTree: createRootRoute({ component: trail }), history: createMemoryHistory() })
  return render(<RouterProvider router={router} />)
}

test('a current page that truncates is a button that shows its full name in a popover', async () => {
  const screen = await renderWorkOrderTrail(320)
  const current = screen.getByRole('button', { name: workOrder })
  await expect.element(current).toHaveAttribute('aria-current', 'page')

  await current.click()

  await expect.element(screen.getByRole('dialog', { name: 'Current page' })).toHaveTextContent(workOrder)
})

test('a current page that truncates is a 44 px touch target, and its focus ring keeps space on each side of the text', async () => {
  const screen = await renderWorkOrderTrail(320)
  const current = screen.getByRole('button', { name: workOrder })
  await expect.element(current).toBeVisible()
  expect(current.element().getBoundingClientRect().height).toBeGreaterThanOrEqual(44)

  const parts = [current.element(), ...current.element().querySelectorAll('*')]
  const shadows = parts.map((el) => getComputedStyle(el).boxShadow)

  await userEvent.keyboard('{Tab}{Tab}')

  await expect.element(current).toHaveFocus()
  const ring = parts.find((el, i) => getComputedStyle(el).boxShadow !== shadows[i])!
  const text = document.createRange()
  text.selectNodeContents(document.createTreeWalker(current.element(), NodeFilter.SHOW_TEXT).nextNode()!)
  const textBox = text.getBoundingClientRect()
  const ringBox = ring.getBoundingClientRect()
  expect(textBox.left - ringBox.left).toBeGreaterThanOrEqual(2)
  expect(textBox.top - ringBox.top).toBeGreaterThanOrEqual(2)
  expect(ringBox.bottom - textBox.bottom).toBeGreaterThanOrEqual(2)
})

test('a current page that fits is a disabled link, not a button', async () => {
  const screen = await renderWorkOrderTrail(1280)

  await expect.element(screen.getByRole('link', { name: workOrder })).toHaveAttribute('aria-disabled', 'true')
  expect(screen.getByRole('button', { name: workOrder }).query()).toBeNull()
})

test('a current page that truncated under an earlier name is a link when it fits', async () => {
  const trail = (width: number, name: string) => (
    <header style={{ width }} className="flex h-16 items-center gap-2">
      <div className="flex min-w-0 items-center gap-2 px-4">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbPage>{name}</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
      </div>
    </header>
  )
  const screen = await render(trail(160, workOrder))
  await expect.element(screen.getByRole('button', { name: workOrder })).toBeVisible()

  await screen.rerender(trail(1280, 'Home'))
  await expect.element(screen.getByRole('link', { name: 'Home' })).toBeVisible()
  await screen.rerender(trail(1280, workOrder))

  await expect.element(screen.getByRole('link', { name: workOrder })).toHaveAttribute('aria-disabled', 'true')
  expect(screen.getByRole('button', { name: workOrder }).query()).toBeNull()
})

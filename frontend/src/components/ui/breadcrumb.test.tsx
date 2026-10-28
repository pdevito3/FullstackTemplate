import { expect, test } from 'vitest'
import { render } from 'vitest-browser-react'

import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage } from '@/components/ui/breadcrumb'

test.each([320, 375])('a long trail stays on one line in a %i px header and truncates only the current page', async (width) => {
  const screen = await render(
    <header style={{ width }} className="flex h-16 items-center gap-2">
      <div className="flex min-w-0 items-center gap-2 px-4">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink href="/">Work orders</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbItem>
              <BreadcrumbPage>T-101 work order for the north yard compressor</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
      </div>
    </header>,
  )
  await expect.element(screen.getByRole('link', { name: 'Work orders' })).toBeVisible()

  const header = screen.container.querySelector('header')!
  const crumb = screen.getByRole('link', { name: 'Work orders' }).element()
  const crumbItem = crumb.closest('li')!
  const current = screen.container.querySelector<HTMLElement>('[data-slot="breadcrumb-page"]')!
  const lineHeight = parseFloat(getComputedStyle(current).lineHeight)

  expect(screen.getByRole('list').element().getBoundingClientRect().height).toBeLessThanOrEqual(lineHeight)
  expect(current.getBoundingClientRect().height).toBeLessThanOrEqual(lineHeight)
  expect(crumb.getBoundingClientRect().height).toBeLessThanOrEqual(lineHeight)
  expect(crumbItem.scrollWidth).toBeLessThanOrEqual(crumbItem.clientWidth)
  expect(current.scrollWidth).toBeGreaterThan(current.clientWidth)
  expect(getComputedStyle(current).textOverflow).toBe('ellipsis')
  expect(current.getBoundingClientRect().right).toBeLessThanOrEqual(header.getBoundingClientRect().right)
})

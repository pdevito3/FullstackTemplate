import { expect, test } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'

test('the menu of a trigger as wide as the page fits in the page', async () => {
  const screen = await render(
    <DropdownMenuTrigger>
      <Button className="w-full">Actions</Button>
      <DropdownMenu>
        <DropdownMenuItem>Edit</DropdownMenuItem>
      </DropdownMenu>
    </DropdownMenuTrigger>,
  )
  expect(screen.getByRole('button').element().getBoundingClientRect().width).toBe(document.documentElement.clientWidth)

  await screen.getByRole('button', { name: 'Actions' }).click()
  await expect.element(page.getByRole('menu')).toBeVisible()

  const content = document.querySelector<HTMLElement>('[data-slot="dropdown-menu-content"]')!
  await Promise.all(content.getAnimations().map((animation) => animation.finished))
  expect(content.getBoundingClientRect().left).toBeGreaterThanOrEqual(0)
  expect(content.getBoundingClientRect().right).toBeLessThanOrEqual(document.documentElement.clientWidth)
  expect(document.documentElement.scrollWidth).toBe(document.documentElement.clientWidth)
})

import { expect, test } from 'vitest'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import { buttonVariants } from '@/components/ui/button-variants'

test.each(['light', 'dark'])('a link styled with buttonVariants has the border of an outline Button in the %s theme', async (theme) => {
  const screen = await render(
    <div className={theme}>
      <Button variant="outline">Save</Button>
      <a href="#details" className={buttonVariants({ variant: 'outline' })}>
        Details
      </a>
    </div>,
  )
  const buttonBorder = getComputedStyle(screen.getByRole('button', { name: 'Save' }).element()).borderTopColor
  const linkBorder = getComputedStyle(screen.getByRole('link', { name: 'Details' }).element()).borderTopColor

  expect(buttonBorder).not.toBe('rgba(0, 0, 0, 0)')
  expect(linkBorder).toBe(buttonBorder)
})

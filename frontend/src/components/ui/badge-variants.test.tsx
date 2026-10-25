import { expect, test } from 'vitest'
import { render } from 'vitest-browser-react'

import { Badge } from '@/components/ui/badge'
import { badgeVariants } from '@/components/ui/badge-variants'

test.each(['light', 'dark'])('a link styled with badgeVariants has the border of an outline Badge in the %s theme', async (theme) => {
  const screen = await render(
    <div className={theme}>
      <Badge variant="outline">Beta</Badge>
      <a href="#beta" className={badgeVariants({ variant: 'outline' })}>
        Release notes
      </a>
    </div>,
  )
  const badgeBorder = getComputedStyle(screen.getByText('Beta').element()).borderTopColor
  const linkBorder = getComputedStyle(screen.getByRole('link', { name: 'Release notes' }).element()).borderTopColor

  expect(badgeBorder).not.toBe('rgba(0, 0, 0, 0)')
  expect(linkBorder).toBe(badgeBorder)
})

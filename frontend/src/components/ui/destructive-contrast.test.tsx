import { CalendarDate } from '@internationalized/date'
import { expect, test } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Badge } from '@/components/ui/badge'
import { badgeVariants } from '@/components/ui/badge-variants'
import { Button } from '@/components/ui/button'
import { JollyCalendar } from '@/components/ui/calendar'
import { JollyDateField } from '@/components/ui/datefield'
import { contrastOf, focusRingContrastOf } from '@/test/contrast'

// WCAG 1.4.3 asks 4.5:1 for text below 18 px. Each surface below shows the destructive color as 14 px text.
const minimumRatio = 4.5

function Surfaces({ theme }: { theme: 'light' | 'dark' }) {
  return (
    <div className={`${theme} bg-background p-4`}>
      <JollyDateField
        label="Due date"
        isInvalid
        defaultValue={new CalendarDate(2026, 3, 10)}
        errorMessage="The date is not valid."
      />
      <Button variant="destructive">Delete</Button>
      <Badge variant="destructive">Failed</Badge>
      <JollyCalendar aria-label="Due" isInvalid value={new CalendarDate(2026, 3, 10)} />
    </div>
  )
}

for (const theme of ['light', 'dark'] as const) {
  test(`destructive color: each destructive text surface has a contrast of 4.5:1 in the ${theme} theme`, async () => {
    const screen = await render(<Surfaces theme={theme} />)
    const ratios: Record<string, number> = {}

    ratios['invalid date segment'] = contrastOf(screen.getByRole('spinbutton', { name: /^month, / }).element())
    await screen.getByRole('spinbutton', { name: /^day, / }).click()
    ratios['focused invalid date segment'] = contrastOf(screen.getByRole('spinbutton', { name: /^day, / }).element())
    ratios['field error message'] = contrastOf(screen.getByText('The date is not valid.').element())
    const button = screen.getByRole('button', { name: 'Delete' }).element()
    ratios['destructive button'] = contrastOf(button)
    await userEvent.hover(button)
    await Promise.all(button.getAnimations().map((transition) => transition.finished))
    ratios['hovered destructive button'] = contrastOf(button)
    ratios['destructive badge'] = contrastOf(screen.getByText('Failed').element())
    ratios['invalid calendar day'] = contrastOf(screen.getByRole('button', { name: /March 10, 2026/ }).element())

    expect(Object.entries(ratios).filter(([, ratio]) => ratio < minimumRatio)).toEqual([])
  })
}

// WCAG 1.4.11 asks 3:1 for a focus indicator against the colors next to it.
const minimumRingRatio = 3

for (const theme of ['light', 'dark'] as const) {
  test(`destructive color: the focus ring of a destructive button and badge has a contrast of 3:1 in the ${theme} theme`, async () => {
    await render(
      <div className={`${theme} bg-background p-4`}>
        <Button variant="destructive">Delete</Button>
        <a href="#failed" className={badgeVariants({ variant: 'destructive' })}>
          Failed
        </a>
      </div>,
    )
    const ratios: Record<string, number> = {}

    for (const [name, element] of [
      ['destructive button', page.getByRole('button', { name: 'Delete' })],
      ['destructive badge link', page.getByRole('link', { name: 'Failed' })],
    ] as const) {
      await userEvent.tab()
      await expect.element(element).toHaveFocus()
      await Promise.all(element.element().getAnimations().map((transition) => transition.finished))
      ratios[name] = focusRingContrastOf(element.element())
    }

    expect(Object.entries(ratios).filter(([, ratio]) => ratio < minimumRingRatio)).toEqual([])
  })
}

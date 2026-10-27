import { CalendarDate } from '@internationalized/date'
import { expect, test } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { JollyCalendar } from '@/components/ui/calendar'
import { JollyDateField } from '@/components/ui/datefield'
import { contrastOf } from '@/test/contrast'

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

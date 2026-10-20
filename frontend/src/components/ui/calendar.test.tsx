import { useState } from 'react'
import { CalendarDate } from '@internationalized/date'
import { Dialog, type DateValue } from 'react-aria-components'
import { expect, test } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import { JollyCalendar } from '@/components/ui/calendar'
import { Popover, PopoverTrigger } from '@/components/ui/popover'

// The same shape as the Pick a date popover of the components showcase.
function DatePopover() {
  const [date, setDate] = useState<DateValue>(new CalendarDate(2024, 3, 15))
  return (
    <PopoverTrigger>
      <Button>Pick a date</Button>
      <Popover className="w-auto p-3" placement="bottom start">
        <Dialog aria-label="Pick a date" className="outline-none">
          <JollyCalendar value={date} onChange={setDate} />
        </Dialog>
      </Popover>
    </PopoverTrigger>
  )
}

function hasFocus(element: Element): boolean {
  return element.contains(document.activeElement)
}

test('calendar zoom: Select month moves the focus into the month picker, a month pick moves it to a day cell', async () => {
  const screen = await render(<DatePopover />)
  await screen.getByRole('button', { name: 'Pick a date' }).click()
  const popover = page.getByRole('dialog', { name: 'Pick a date' })
  await expect.element(popover.getByRole('grid')).toBeVisible()

  await popover.getByRole('button', { name: 'Select month' }).click()
  const monthPicker = popover.getByRole('listbox', { name: 'Month picker' })
  await expect.element(monthPicker.getByRole('option', { name: 'Mar' })).toHaveAttribute('aria-selected', 'true')
  await expect.poll(() => hasFocus(monthPicker.element())).toBe(true)

  await monthPicker.getByRole('option', { name: 'Jun' }).click()
  await expect.element(popover.getByRole('button', { name: 'Select month' })).toHaveTextContent('June 2024')
  await expect.poll(() => document.activeElement?.closest('[role="gridcell"]')).not.toBeNull()
})

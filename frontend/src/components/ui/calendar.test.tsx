import { useState } from 'react'
import { CalendarDate } from '@internationalized/date'
import { Dialog, type DateValue, type RangeValue } from 'react-aria-components'
import { expect, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Button } from '@/components/ui/button'
import { JollyCalendar, JollyRangeCalendar } from '@/components/ui/calendar'
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

function DateRange({ initialRange }: { initialRange: RangeValue<DateValue> }) {
  const [range, setRange] = useState<RangeValue<DateValue> | null>(initialRange)
  return (
    <>
      <JollyRangeCalendar aria-label="Dates" value={range} onChange={setRange} />
      <output aria-label="Range">{range ? `${range.start} to ${range.end}` : ''}</output>
    </>
  )
}

const august2026 = { start: new CalendarDate(2026, 8, 1), end: new CalendarDate(2026, 8, 31) }

test('range calendar: Enter on the start day keeps the focus on that day', async () => {
  const screen = await render(<DateRange initialRange={august2026} />)
  screen.getByRole('button', { name: /August 15, 2026/ }).element().focus()

  await userEvent.keyboard('{Enter}')
  await expect.poll(() => document.activeElement?.getAttribute('aria-label')).toMatch(/August 15, 2026/)
  await expect.poll(() => screen.container.querySelector('[aria-live="polite"]')?.textContent).toBe('Choose an end date.')
  await userEvent.keyboard('{ArrowRight}{ArrowRight}{ArrowRight}{Enter}')

  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-08-15 to 2026-08-18')
  expect(screen.container.querySelector('[aria-live="polite"]')?.textContent).toBe('')
  expect(screen.getByRole('button', { name: /Tuesday, August 18, 2026 selected$/ }).element()).toHaveAttribute('data-focused')
})

test('range calendar: Enter on the end day in the next month keeps the focus and the month of that day', async () => {
  const screen = await render(<DateRange initialRange={august2026} />)
  screen.getByRole('button', { name: /August 15, 2026/ }).element().focus()

  await userEvent.keyboard('{Enter}{ArrowDown}{ArrowDown}{ArrowRight}{ArrowRight}{ArrowRight}{ArrowRight}{ArrowRight}{Enter}')

  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-08-15 to 2026-09-03')
  await expect.element(screen.getByRole('button', { name: 'Select month' })).toHaveTextContent('September 2026')
  await expect.poll(() => document.activeElement?.getAttribute('aria-label')).toMatch(/Thursday, September 3, 2026 selected$/)
})

test('range calendar: a click on the end day in the next month keeps that month', async () => {
  const screen = await render(<DateRange initialRange={august2026} />)

  await screen.getByRole('button', { name: /August 15, 2026/ }).click()
  // React Aria puts a second, hidden Next button after the grid for screen readers.
  await screen.getByRole('button', { name: 'Next', exact: true }).first().click()
  await screen.getByRole('button', { name: /September 3, 2026/ }).click()

  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-08-15 to 2026-09-03')
  await expect.element(screen.getByRole('button', { name: 'Select month' })).toHaveTextContent('September 2026')
  expect(document.activeElement?.getAttribute('aria-label')).toMatch(/Thursday, September 3, 2026 selected$/)
})

function DateRangeWithControls() {
  const [range, setRange] = useState<RangeValue<DateValue> | null>(august2026)
  return <JollyRangeCalendar aria-label="Dates" value={range} onChange={setRange} showQuickSelects showDateInput />
}

test('range calendar: a Quick Select shows the month of the start of its range', async () => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date(2026, 7, 15, 12))
  try {
    const screen = await render(<DateRangeWithControls />)

    await screen.getByRole('button', { name: 'Last Year', exact: true }).click()

    await expect.element(screen.getByRole('button', { name: 'Select month' })).toHaveTextContent('January 2025')
  } finally {
    vi.useRealTimers()
  }
})

test('range calendar: a typed start date shows its month', async () => {
  const screen = await render(<DateRangeWithControls />)

  await screen.getByRole('spinbutton', { name: 'month, Start date' }).click()
  await userEvent.keyboard('03102026')

  await expect.element(screen.getByRole('group', { name: 'Start date' })).toHaveTextContent('3/10/2026')
  await expect.element(screen.getByRole('button', { name: 'Select month' })).toHaveTextContent('March 2026')
})

async function pixelsOf(element: Element): Promise<ImageData> {
  const image = new Image()
  image.src = `data:image/png;base64,${await page.screenshot({ element, save: false })}`
  await image.decode()
  const canvas = document.createElement('canvas')
  canvas.width = image.width
  canvas.height = image.height
  const context = canvas.getContext('2d')!
  context.drawImage(image, 0, 0)
  return context.getImageData(0, 0, image.width, image.height)
}

test('range calendar: a selected range paints one band from its start day, with no gap between the days', async () => {
  const screen = await render(<DateRange initialRange={{ start: new CalendarDate(2026, 8, 3), end: new CalendarDate(2026, 8, 28) }} />)
  const grid = screen.getByRole('grid').element()
  const pixels = await pixelsOf(grid)
  const scale = pixels.width / grid.getBoundingClientRect().width
  const colorAt = (x: number, y: number) => {
    const gridBox = grid.getBoundingClientRect()
    const i = (Math.floor((y - gridBox.top) * scale) * pixels.width + Math.floor((x - gridBox.left) * scale)) * 4
    return Array.from(pixels.data.slice(i, i + 4))
  }
  const day = (date: number) => screen.getByRole('button', { name: new RegExp(`August ${date}, 2026`) }).element().getBoundingClientRect()

  const start = day(3)
  expect(day(7).top, 'the start day and the next four days are in one row').toBe(start.top)
  const middle = start.top + start.height / 2
  expect(colorAt(start.right - 0.5, middle), 'the right edge of the start day').toEqual(colorAt(start.left + 3, middle))
  expect(colorAt(day(4).left + 0.5, middle), 'the left edge of Aug 4').toEqual(colorAt(day(4).left + 3, middle))
  for (const date of [4, 5, 6]) {
    const top = day(date).top + 1.5
    const band = colorAt(day(date).left + day(date).width / 2, top)

    expect(colorAt(day(date).right - 0.5, top), `the right edge of Aug ${date}`).toEqual(band)
    expect(colorAt(day(date + 1).left + 0.5, top), `the left edge of Aug ${date + 1}`).toEqual(band)
  }
  expect(colorAt(start.left + start.width / 2, start.top + 0.5), 'the top edge of the start day').not.toEqual(colorAt(start.left + start.width / 2, start.top - 1))
})

test('calendar: the wide layout is as wide with the range quick selects as with the single date quick selects', async () => {
  const screen = await render(
    <>
      <div role="region" aria-label="Single" className="w-fit">
        <JollyCalendar aria-label="Date" showQuickSelects layout="wide" />
      </div>
      <div role="region" aria-label="Range" className="w-fit">
        <JollyRangeCalendar aria-label="Dates" showQuickSelects layout="wide" />
      </div>
    </>
  )
  await document.fonts.ready

  const single = screen.getByRole('region', { name: 'Single' }).element().getBoundingClientRect()
  const range = screen.getByRole('region', { name: 'Range' }).element().getBoundingClientRect()

  expect(range.width).toBe(single.width)
  for (const region of ['Single', 'Range']) {
    for (const preset of screen.getByRole('region', { name: region }).getByRole('button', { name: /^(This|Last|Next|Today|Tomorrow|Yesterday)/ }).elements()) {
      expect(contentFits(preset), `the ${preset.textContent} label fits its button`).toBe(true)
    }
  }
})

function contentFits(button: Element): boolean {
  const text = document.createRange()
  text.selectNodeContents(button)
  const style = getComputedStyle(button)
  const room = button.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight)
  return text.getBoundingClientRect().width <= room
}

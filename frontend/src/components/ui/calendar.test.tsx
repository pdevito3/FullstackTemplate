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
  const [date, setDate] = useState<DateValue | null>(new CalendarDate(2024, 3, 15))
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
  await screen.getByRole('banner').getByRole('button', { name: 'Next', exact: true }).click()
  await screen.getByRole('button', { name: /September 3, 2026/ }).click()

  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-08-15 to 2026-09-03')
  await expect.element(screen.getByRole('button', { name: 'Select month' })).toHaveTextContent('September 2026')
  expect(document.activeElement?.getAttribute('aria-label')).toMatch(/Thursday, September 3, 2026 selected$/)
})

test('range calendar: a month pick after the start day keeps the range open', async () => {
  const screen = await render(<DateRange initialRange={august2026} />)

  await screen.getByRole('button', { name: /August 15, 2026/ }).click()
  await screen.getByRole('button', { name: 'Select month' }).click()
  await screen.getByRole('listbox', { name: 'Month picker' }).getByRole('option', { name: 'Sep' }).click()
  await screen.getByRole('button', { name: /September 3, 2026/ }).click()

  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-08-15 to 2026-09-03')
})

test('range calendar: a year pick and a month pick after the start day keep the range open', async () => {
  const screen = await render(<DateRange initialRange={august2026} />)

  await screen.getByRole('button', { name: /August 15, 2026/ }).click()
  await screen.getByRole('button', { name: 'Select month' }).click()
  await screen.getByRole('button', { name: 'Select year' }).click()
  await screen.getByRole('listbox', { name: 'Year picker' }).getByRole('option', { name: '2027' }).click()
  await screen.getByRole('listbox', { name: 'Month picker' }).getByRole('option', { name: 'Feb' }).click()
  await screen.getByRole('button', { name: /February 10, 2027/ }).click()

  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-08-15 to 2027-02-10')
})

test('range calendar: a click outside the calendar after a month pick ends the range in the picked month', async () => {
  const screen = await render(
    <>
      <DateRange initialRange={august2026} />
      <button type="button">Outside</button>
    </>
  )

  await screen.getByRole('button', { name: /August 15, 2026/ }).click()
  await screen.getByRole('button', { name: 'Select month' }).click()
  await screen.getByRole('listbox', { name: 'Month picker' }).getByRole('option', { name: 'Sep' }).click()
  await screen.getByRole('button', { name: 'Outside' }).click()

  // A hover in a range moves the focus, so the end day is the September day under the pointer after the pick.
  await expect.poll(() => screen.getByRole('status', { name: 'Range' }).element().textContent).toMatch(/^2026-08-15 to 2026-09-\d\d$/)
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

function EmptyDateRangeWithControls() {
  const [range, setRange] = useState<RangeValue<DateValue> | null>(null)
  return (
    <>
      <JollyRangeCalendar aria-label="Dates" value={range} onChange={setRange} showDateInput />
      <output aria-label="Range">{range ? `${range.start} to ${range.end}` : ''}</output>
    </>
  )
}

test('range calendar: a typed start date in an empty range waits for the end date', async () => {
  const screen = await render(<EmptyDateRangeWithControls />)

  await screen.getByRole('spinbutton', { name: 'month, Start date' }).click()
  await userEvent.keyboard('03102026')

  await expect.element(screen.getByRole('group', { name: 'Start date' })).toHaveTextContent('3/10/2026')
  await expect.element(screen.getByRole('group', { name: 'End date' })).toHaveTextContent('mm/dd/yyyy')
  await expect.element(screen.getByRole('button', { name: 'Select month' })).toHaveTextContent('March 2026')
  await expect.element(screen.getByRole('status', { name: 'Range' })).toBeEmptyDOMElement()

  await screen.getByRole('spinbutton', { name: 'month, End date' }).click()
  await userEvent.keyboard('03202026')

  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-03-10 to 2026-03-20')
})

test('range calendar: a typed end date in an empty range keeps the date', async () => {
  const screen = await render(<EmptyDateRangeWithControls />)

  await screen.getByRole('spinbutton', { name: 'month, End date' }).click()
  await userEvent.keyboard('03202026')

  await expect.element(screen.getByRole('group', { name: 'End date' })).toHaveTextContent('3/20/2026')
  await expect.element(screen.getByRole('group', { name: 'Start date' })).toHaveTextContent('mm/dd/yyyy')
  await expect.element(screen.getByRole('status', { name: 'Range' })).toBeEmptyDOMElement()
})

function TypedDateRange({ initialRange = null }: { initialRange?: RangeValue<DateValue> | null }) {
  const [range, setRange] = useState<RangeValue<DateValue> | null>(initialRange)
  return (
    <>
      <JollyRangeCalendar aria-label="Dates" value={range} onChange={setRange} showDateInput />
      <output aria-label="Range">{range ? `${range.start} to ${range.end}` : ''}</output>
    </>
  )
}

async function typeDate(field: string, digits: string) {
  await page.getByRole('spinbutton', { name: `month, ${field}` }).click()
  await userEvent.keyboard(digits)
}

async function clearDate(field: string) {
  for (const part of ['year', 'day', 'month']) {
    await page.getByRole('spinbutton', { name: `${part}, ${field}` }).click()
    await userEvent.keyboard('{Backspace}{Backspace}{Backspace}{Backspace}')
  }
}

const lastWeek = { start: new CalendarDate(2026, 8, 2), end: new CalendarDate(2026, 8, 8) }

test('range calendar: a typed start date that is cleared again leaves the range empty after a typed end date', async () => {
  const screen = await render(<TypedDateRange />)

  await typeDate('Start date', '03102026')
  await clearDate('Start date')
  await typeDate('End date', '03202026')

  await expect.element(screen.getByRole('group', { name: 'Start date' })).toHaveTextContent('mm/dd/yyyy')
  await expect.element(screen.getByRole('button', { name: 'Select month' })).toHaveTextContent('March 2026')
  await expect.element(screen.getByRole('status', { name: 'Range' })).toBeEmptyDOMElement()
})

test('range calendar: a cleared start date clears a set range', async () => {
  const screen = await render(<TypedDateRange initialRange={lastWeek} />)

  await clearDate('Start date')

  await expect.element(screen.getByRole('group', { name: 'End date' })).toHaveTextContent('8/8/2026')
  await expect.element(screen.getByRole('status', { name: 'Range' })).toBeEmptyDOMElement()
})

test('range calendar: a start date typed after the end date waits for its four-digit year', async () => {
  const screen = await render(<TypedDateRange />)

  await typeDate('End date', '03202026')
  await typeDate('Start date', '03102')
  await expect.element(screen.getByRole('status', { name: 'Range' })).toBeEmptyDOMElement()
  await userEvent.keyboard('026')

  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-03-10 to 2026-03-20')
})

test('range calendar: an end date with a short year leaves the range empty', async () => {
  const screen = await render(<TypedDateRange />)

  await typeDate('Start date', '03102026')
  await typeDate('End date', '032020')
  await userEvent.keyboard('{Tab}')

  await expect.element(screen.getByRole('group', { name: 'End date' })).toHaveTextContent('3/20/20')
  await expect.element(screen.getByRole('status', { name: 'Range' })).toBeEmptyDOMElement()
})

test('range calendar: two-digit years leave the range empty', async () => {
  const screen = await render(<TypedDateRange />)

  await typeDate('Start date', '031026')
  await userEvent.keyboard('{Tab}')
  await typeDate('End date', '032026')
  await userEvent.keyboard('{Tab}')

  await expect.element(screen.getByRole('status', { name: 'Range' })).toBeEmptyDOMElement()
})

test('range calendar: a typed end date before the start date leaves the range empty and marks the end date', async () => {
  const screen = await render(<TypedDateRange initialRange={lastWeek} />)

  await typeDate('End date', '07012026')

  await expect.element(screen.getByRole('group', { name: 'End date' })).toHaveTextContent('7/1/2026')
  await expect.element(screen.getByRole('spinbutton', { name: 'month, End date' })).toHaveAttribute('aria-invalid', 'true')
  await expect.element(screen.getByRole('spinbutton', { name: 'month, Start date' })).not.toHaveAttribute('aria-invalid')
  await expect.element(screen.getByRole('status', { name: 'Range' })).toBeEmptyDOMElement()

  await typeDate('End date', '08202026')

  await expect.element(screen.getByRole('spinbutton', { name: 'month, End date' })).not.toHaveAttribute('aria-invalid')
  await expect.element(screen.getByRole('status', { name: 'Range' })).toHaveTextContent('2026-08-02 to 2026-08-20')
})

function TypedDate() {
  const [date, setDate] = useState<DateValue | null>(null)
  return (
    <>
      <JollyCalendar aria-label="Dates" value={date} onChange={setDate} showDateInput />
      <output aria-label="Value">{date?.toString() ?? ''}</output>
    </>
  )
}

test('calendar: a typed date waits for its four-digit year, and a cleared date clears the value', async () => {
  const screen = await render(<TypedDate />)

  await typeDate('Date', '031020')
  await expect.element(screen.getByRole('status', { name: 'Value' })).toBeEmptyDOMElement()
  await userEvent.keyboard('26')
  await expect.element(screen.getByRole('status', { name: 'Value' })).toHaveTextContent('2026-03-10')

  await clearDate('Date')

  await expect.element(screen.getByRole('group', { name: 'Date' })).toHaveTextContent('mm/dd/yyyy')
  await expect.element(screen.getByRole('button', { name: 'Select month' })).toHaveTextContent('March 2026')
  await expect.element(screen.getByRole('status', { name: 'Value' })).toBeEmptyDOMElement()
})

for (const { name, picker, open } of [
  { name: 'month picker', picker: 'Month picker', open: ['Select month'] },
  { name: 'year picker', picker: 'Year picker', open: ['Select month', 'Select year'] },
]) {
  test(`calendar: the ${name} fills the height of the days view with fixedHeight`, async () => {
    const screen = await render(<JollyRangeCalendar aria-label="Dates" fixedHeight />)
    const days = screen.getByRole('grid').element().getBoundingClientRect()

    for (const button of open) await screen.getByRole('button', { name: button }).click()

    const options = screen.getByRole('listbox', { name: picker }).getByRole('option').elements()
    expect(options).toHaveLength(12)
    const bottom = Math.max(...options.map((option) => option.getBoundingClientRect().bottom))
    expect(Math.abs(bottom - days.bottom), 'the last row of the picker ends at the bottom of the days').toBeLessThanOrEqual(4)
  })
}

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

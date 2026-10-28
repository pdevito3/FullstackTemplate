import { expect, test, vi } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { JollyDatePicker, JollyDateRangePicker } from '@/components/ui/date-picker'
import { parseTimeZone } from '@/lib/zoned-time'

const shortYearMessage = 'Enter a 4-digit year.'

test('date range picker: a short year is marked after the focus leaves the picker, until the year has four digits', async () => {
  const screen = await render(
    <>
      <JollyDateRangePicker label="Stay" />
      <button type="button">Next</button>
    </>,
  )
  const startYear = screen.getByRole('spinbutton', { name: 'year, Start Date, Stay' })

  await page.getByRole('spinbutton', { name: 'month, Start Date, Stay' }).click()
  await userEvent.keyboard('031026{Tab}03122026')
  await expect.element(startYear).not.toHaveAttribute('aria-invalid')
  await page.getByRole('button', { name: 'Next' }).click()

  await expect.element(startYear).toHaveAttribute('aria-invalid', 'true')
  // React Aria also describes the segments with the selected range.
  await expect.element(startYear).toHaveAccessibleDescription(/Enter a 4-digit year\./)
  await expect.element(screen.getByRole('alert')).toHaveTextContent(shortYearMessage)

  await startYear.click()
  await userEvent.keyboard('2026')

  await expect.element(startYear).not.toHaveAttribute('aria-invalid')
  await expect.element(screen.getByText(shortYearMessage)).not.toBeInTheDocument()
})

test('date picker: with no value, opens on the month of today at the place, not in the zone of the browser', async () => {
  // Arrange
  vi.useFakeTimers({ toFake: ['Date'] })
  // 05:00 UTC on September 1 is August 31 in Pago Pago, and September 1 in Kiritimati, the zone of the component tests.
  vi.setSystemTime(new Date('2026-09-01T05:00:00Z'))
  try {
    const screen = await render(<JollyDatePicker label="Start date" timeZone={parseTimeZone('Pacific/Pago_Pago')} />)

    // Act
    await screen.getByRole('button', { name: /Calendar/ }).click()

    // Assert
    await expect.element(page.getByRole('dialog').getByRole('heading')).toHaveTextContent('August 2026')
  } finally {
    vi.useRealTimers()
  }
})

test('date range picker: with no value, opens on the month of today at the place, not in the zone of the browser', async () => {
  // Arrange
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-09-01T05:00:00Z'))
  try {
    const screen = await render(<JollyDateRangePicker label="Stay" timeZone={parseTimeZone('Pacific/Pago_Pago')} />)

    // Act
    await screen.getByRole('button', { name: /Calendar/ }).click()

    // Assert
    await expect.element(page.getByRole('dialog').getByRole('heading')).toHaveTextContent('August 2026')
  } finally {
    vi.useRealTimers()
  }
})

test('date picker: with a time granularity and no value, renders its time segments for a place', async () => {
  // Act
  const screen = await render(
    <JollyDatePicker label="Starts at" granularity="minute" timeZone={parseTimeZone('Pacific/Pago_Pago')} />,
  )

  // Assert
  await expect.element(screen.getByRole('spinbutton', { name: 'minute, Starts at' })).toBeVisible()
})

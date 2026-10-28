import { expect, test } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { JollyDateRangePicker } from '@/components/ui/date-picker'

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

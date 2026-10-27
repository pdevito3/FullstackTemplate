import { expect, test } from 'vitest'
import { page, userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { JollyDateField } from '@/components/ui/datefield'

const shortYearMessage = 'Enter a 4-digit year.'

test('date field: a short year is marked after the focus leaves the field, until the year has four digits', async () => {
  const screen = await render(
    <>
      <JollyDateField label="Due date" />
      <button type="button">Next</button>
    </>,
  )
  const year = screen.getByRole('spinbutton', { name: 'year, Due date' })

  await page.getByRole('spinbutton', { name: 'month, Due date' }).click()
  await userEvent.keyboard('0310999')
  await expect.element(year).not.toHaveAttribute('aria-invalid')
  await userEvent.keyboard('{Tab}')

  await expect.element(year).toHaveAttribute('aria-invalid', 'true')
  // React Aria also describes the segments with the selected date.
  await expect.element(year).toHaveAccessibleDescription(/Enter a 4-digit year\./)
  await expect.element(screen.getByRole('alert')).toHaveTextContent(shortYearMessage)

  await year.click()
  await userEvent.keyboard('2026')

  await expect.element(year).not.toHaveAttribute('aria-invalid')
  await expect.element(screen.getByText(shortYearMessage)).not.toBeInTheDocument()
})

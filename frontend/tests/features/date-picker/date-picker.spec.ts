import { test, expect } from '@tests/support/fixtures'
import { datePickerDemoPage } from './date-picker.tasks'

const shortYear = 'Enter a 4-digit year.'

test.describe('the date picker', () => {
  test('marks a short year with a red message and border after the focus leaves it, until the year has four digits', async ({ page }) => {
    const demo = datePickerDemoPage(page)
    await demo.open()

    await demo.typeDate('Start date', '0310999')
    await expect(demo.at.segment('Start date', 'year')).not.toHaveAttribute('aria-invalid')
    await demo.leavePicker()

    const year = demo.at.segment('Start date', 'year')
    await expect(year).toHaveAttribute('aria-invalid', 'true')
    // React Aria also describes the segments with the selected date.
    await expect(year).toHaveAccessibleDescription(/Enter a 4-digit year\./)
    const red = await demo.textColorOf(demo.at.message(shortYear))
    await expect(demo.at.picker('Start date')).toHaveCSS('border-top-color', red)

    await demo.typeDate('Start date', '03102026')

    await expect(year).not.toHaveAttribute('aria-invalid')
    await expect(demo.at.message(shortYear)).toHaveCount(0)
  })
})

import { expect, type Locator, type Page } from '@playwright/test'
import { datePickerDemo } from './date-picker.locators'

export function datePickerDemoPage(page: Page) {
  const at = datePickerDemo(page)

  return {
    at,

    async open() {
      await page.goto('/components')
      await expect(at.heading).toBeVisible()
    },

    async typeDate(label: string, digits: string) {
      await at.segment(label, 'month').click()
      await page.keyboard.type(digits)
    },

    /** Tabs past the calendar button of the picker to the next field of the form. */
    async leavePicker() {
      await page.keyboard.press('Tab')
      await page.keyboard.press('Tab')
      await expect(at.acceptTerms).toBeFocused()
    },

    async textColorOf(element: Locator) {
      return element.evaluate((node) => getComputedStyle(node).color)
    },
  }
}

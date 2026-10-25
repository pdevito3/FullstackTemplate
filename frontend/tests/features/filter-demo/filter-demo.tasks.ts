import { expect, type Page } from '@playwright/test'
import { filterDemo } from './filter-demo.locators'

export function filterDemoPage(page: Page) {
  const at = filterDemo(page)

  return {
    at,

    async open() {
      await page.goto('/filter-demo')
      await expect(at.heading).toBeVisible()
    },

    /** Opens the filter control of `property` from the Add Filter menu, and waits until the control loads. */
    async chooseProperty(property: string) {
      await at.addFilterMenu.click()
      await at.property(property).click()
      await expect(at.control(property)).toBeVisible()
      await expect(at.loadingControl).toHaveCount(0)
    },

    async addTodayFilter(property: string) {
      await at.quickSelect(property, 'Today').click()
      await at.addFilter(property).click()
    },

    async pageScrollWidth() {
      return page.evaluate(() => document.documentElement.scrollWidth)
    },
  }
}

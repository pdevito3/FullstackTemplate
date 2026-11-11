import { expect, type Page } from '@playwright/test'
import { sharedComponentsPage } from '@tests/support/shared-components'
import { filterBuilderDemo } from './filter-builder.locators'

export function filterBuilderDemoPage(page: Page) {
  const at = filterBuilderDemo(page)

  return {
    at,

    async open() {
      await page.goto(sharedComponentsPage)
      await expect(at.heading).toBeVisible()
    },

    async chooseProperty(propertyLabel: string) {
      await at.addFilterMenu.click()
      await at.property(propertyLabel).click()
    },
  }
}

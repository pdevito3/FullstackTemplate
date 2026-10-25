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

    async chooseProperty(propertyLabel: string) {
      await at.addFilterMenu.click()
      await at.property(propertyLabel).click()
      await expect(at.control(propertyLabel)).toBeVisible()
      await expect(at.loadingControl).toHaveCount(0)
    },

    async addTodayFilter(propertyLabel: string) {
      await at.quickSelect(propertyLabel, 'Today').click()
      await at.addFilter(propertyLabel).click()
    },

    async cssFiltersAround(propertyLabel: string) {
      return at.control(propertyLabel).evaluate((control) => {
        const filters: string[] = []
        for (let element: Element | null = control; element; element = element.parentElement) {
          const { filter } = getComputedStyle(element)
          if (filter !== 'none') filters.push(filter)
        }
        return filters
      })
    },

    async pageScrollWidth() {
      return page.evaluate(() => document.documentElement.scrollWidth)
    },
  }
}

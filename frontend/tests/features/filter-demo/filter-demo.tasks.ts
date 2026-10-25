import { expect, type Locator, type Page } from '@playwright/test'
import { filterDemo } from './filter-demo.locators'

export function filterDemoPage(page: Page) {
  const at = filterDemo(page)

  return {
    at,

    async open() {
      await page.goto('/filter-demo')
      await expect(at.heading).toBeVisible()
    },

    async openOn(today: Date) {
      await page.clock.setFixedTime(today)
      await page.goto('/filter-demo')
      await expect(at.heading).toBeVisible()
    },

    async chooseProperty(propertyLabel: string) {
      await at.addFilterMenu.click()
      await at.property(propertyLabel).click()
      await expect(at.control(propertyLabel)).toBeVisible()
      await expect(at.loadingControl).toHaveCount(0)
    },

    async chooseBetween(propertyLabel: string) {
      await at.dateMode(propertyLabel, 'Between').click()
      await expect(at.dateField(propertyLabel, 'Start date')).toBeVisible()
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

    async scrollControlToEnd(propertyLabel: string) {
      await at.control(propertyLabel).hover()
      await page.mouse.wheel(0, 200)
    },

    /** The bottom edge of the control as the user sees it: the edge of the element that clips it, or its own. */
    async visibleBottomOf(propertyLabel: string) {
      return at.control(propertyLabel).evaluate((control) => {
        for (let element = control.parentElement; element && element !== document.body; element = element.parentElement) {
          if (getComputedStyle(element).overflowY !== 'visible') return element.getBoundingClientRect().bottom
        }
        return control.getBoundingClientRect().bottom
      })
    },

    async sidewaysScrollOf(propertyLabel: string) {
      return at.control(propertyLabel).evaluate((control) => {
        let widest = 0
        for (const element of [control, ...control.querySelectorAll('*')]) {
          if (['auto', 'scroll'].includes(getComputedStyle(element).overflowX)) {
            widest = Math.max(widest, element.scrollWidth - element.clientWidth)
          }
        }
        return widest
      })
    },

    async edgesOf(element: Locator) {
      const box = await element.boundingBox()
      if (!box) throw new Error('The element is not on the page.')
      return { left: box.x, right: box.x + box.width }
    },

    async pageScrollWidth() {
      return page.evaluate(() => document.documentElement.scrollWidth)
    },
  }
}

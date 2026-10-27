import { expect, type Locator, type Page } from '@playwright/test'
import { filterDemo } from './filter-demo.locators'

export function filterDemoPage(page: Page) {
  const at = filterDemo(page)

  const showOtherMonth = async (propertyLabel: string, name: 'Next' | 'Previous') => {
    const shownMonth = await at.selectMonth(propertyLabel).textContent()
    await at.calendarNav(propertyLabel, name).click()
    await expect(at.selectMonth(propertyLabel)).not.toHaveText(shownMonth ?? '')
  }

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

    async closeControl(propertyLabel: string) {
      await at.button(propertyLabel, 'Close').click()
      await expect(at.control(propertyLabel)).toBeHidden()
    },

    async chooseBetween(propertyLabel: string) {
      await at.button(propertyLabel, 'Between').click()
      await expect(at.dateField(propertyLabel, 'Start date')).toBeVisible()
    },

    async showNextMonth(propertyLabel: string) {
      await showOtherMonth(propertyLabel, 'Next')
    },

    async showPreviousMonth(propertyLabel: string) {
      await showOtherMonth(propertyLabel, 'Previous')
    },

    async openMonthPicker(propertyLabel: string) {
      await at.selectMonth(propertyLabel).click()
      await expect(at.monthOption(propertyLabel, 'Jan')).toBeVisible()
    },

    async pickMonth(propertyLabel: string, name: string) {
      await at.monthOption(propertyLabel, name).click()
      await expect(at.selectMonth(propertyLabel)).toContainText(name)
    },

    async addBooleanFilter(propertyLabel: string, value: 'True' | 'False') {
      await at.addFilterMenu.click()
      await at.property(propertyLabel).click()
      await at.button(propertyLabel, value).click()
      await expect(at.control(propertyLabel)).toBeHidden()
    },

    async editFilter(propertyLabel: string) {
      await at.editFilter(propertyLabel).click()
      await expect(at.control(propertyLabel)).toBeVisible()
    },

    async updateWithQuickSelect(propertyLabel: string, quickSelect: string) {
      await at.button(propertyLabel, quickSelect).click()
      await at.button(propertyLabel, 'Update Filter').click()
      await expect(at.control(propertyLabel)).toBeHidden()
    },

    async addTodayFilter(propertyLabel: string) {
      await at.button(propertyLabel, 'Today').click()
      await at.addFilter(propertyLabel).click()
      await expect(at.control(propertyLabel)).toBeHidden()
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

    /** The space between each side of the sheet and the nearest button or text box of the control, without the Close button. */
    async sideMarginsOf(propertyLabel: string) {
      const sheet = await at.control(propertyLabel).boundingBox()
      const close = await at.button(propertyLabel, 'Close').boundingBox()
      if (!sheet || !close) throw new Error('The sheet is not on the screen.')
      let left = Infinity
      let right = Infinity
      for (const field of await at.fields(propertyLabel).all()) {
        const box = await field.boundingBox()
        if (!box || box.width === 0 || (box.x === close.x && box.y === close.y)) continue
        left = Math.min(left, box.x - sheet.x)
        right = Math.min(right, sheet.x + sheet.width - (box.x + box.width))
      }
      return { left, right }
    },

    async pageScrollWidth() {
      return page.evaluate(() => document.documentElement.scrollWidth)
    },
  }
}

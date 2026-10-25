import { test, expect } from '@tests/support/fixtures'
import { filterDemoPage } from './filter-demo.tasks'

const phone = { width: 390, height: 844 }

test.describe('the Add Filter menu on a phone', () => {
  test.use({ viewport: phone })

  for (const property of ['First Name', 'Created Date', 'Comment Count', 'Is Blocked', 'Status']) {
    test(`shows the ${property} control sharp and fully on the screen`, async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.open()

      await demo.chooseProperty(property)

      await expect(demo.at.control(property)).toBeInViewport({ ratio: 1 })
      expect(await demo.pageScrollWidth()).toBe(phone.width)
      expect(await demo.cssFiltersAround(property)).toEqual([])
    })
  }

  test('adds a date filter from its control', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Created Date')
    await demo.addTodayFilter('Created Date')

    await expect(demo.at.removeFilter('Created Date')).toBeVisible()
    await expect(demo.at.control('Created Date')).toBeHidden()
  })
})

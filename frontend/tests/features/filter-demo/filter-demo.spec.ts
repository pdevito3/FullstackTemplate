import { test, expect } from '@tests/support/fixtures'
import { filterDemoPage } from './filter-demo.tasks'

// A phone in portrait. A control in a submenu beside the Add Filter menu has no room at this width.
const phone = { width: 390, height: 844 }

test.describe('the Add Filter menu on a phone', () => {
  test.use({ viewport: phone })

  for (const property of ['Created Date', 'Comment Count', 'Is Blocked', 'Status']) {
    test(`shows the ${property} control fully on the screen`, async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.open()

      await demo.chooseProperty(property)

      await expect(demo.at.control(property)).toBeInViewport({ ratio: 1 })
      expect(await demo.pageScrollWidth()).toBe(phone.width)
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

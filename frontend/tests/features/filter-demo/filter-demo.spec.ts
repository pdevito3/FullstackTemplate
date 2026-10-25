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

// The date control is the largest. A submenu with it needs about 906 px beside the sidebar, and 474 px of height.
for (const screen of [
  { name: 'a phone on its side', viewport: { width: 844, height: 390 } },
  { name: 'a tablet', viewport: { width: 820, height: 1180 } },
]) {
  test.describe(`the Add Filter menu on ${screen.name}`, () => {
    test.use({ viewport: screen.viewport })

    test('shows the Created Date control fully on the screen', async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.open()

      await demo.chooseProperty('Created Date')

      await expect(demo.at.control('Created Date')).toBeInViewport({ ratio: 1 })
      expect(await demo.pageScrollWidth()).toBe(screen.viewport.width)
    })
  })
}

test.describe('the Add Filter menu on a desktop', () => {
  test.use({ viewport: { width: 1280, height: 800 } })

  test('shows the Created Date control beside the open menu', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Created Date')

    await expect(demo.at.control('Created Date')).toBeInViewport({ ratio: 1 })
    await expect(demo.at.searchProperties).toBeVisible()
  })
})

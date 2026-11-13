import { test, expect } from '@tests/support/fixtures'
import { filterBuilderDemoPage } from './filter-builder.tasks'

// The date control loads its chunk when it first shows.
const dateControlSource = /\/assets\/date-filter-[\w-]+\.js$/

for (const screen of [
  { name: 'desktop', viewport: { width: 1280, height: 800 } },
  { name: 'phone', viewport: { width: 390, height: 844 } },
]) {
  test.describe(`a date filter on a ${screen.name}`, () => {
    test.use({ viewport: screen.viewport })

    test('shows a failed load of its code in the control, and loads it again on Try again', async ({ page, api }) => {
      const reconnect = await api.failPageSource(dateControlSource)
      const demo = filterBuilderDemoPage(page)
      await demo.open()

      await demo.chooseProperty('Due Date')

      await expect(demo.at.loadError('Due Date')).toHaveText(/This filter did not load\.\s*Check your connection, then try again\. If it still does not load, reload the page\./)
      await expect(demo.at.heading).toBeVisible()
      await expect(demo.at.pageError).toHaveCount(0)

      reconnect()
      await demo.at.tryAgain('Due Date').click()

      await expect(demo.at.dateSegment('Due Date', 'month')).toBeVisible()
      await expect(demo.at.loadError('Due Date')).toHaveCount(0)
    })

    test('keeps the focus in the control after Try again, so that Escape still closes it', async ({ page, api }) => {
      const reconnect = await api.failPageSource(dateControlSource)
      const demo = filterBuilderDemoPage(page)
      await demo.open()
      await demo.chooseProperty('Due Date')
      await expect(demo.at.loadError('Due Date')).toBeVisible()

      await demo.at.tryAgain('Due Date').click()

      await expect(demo.at.loadError('Due Date')).toBeVisible()
      await expect(demo.at.tryAgain('Due Date')).toBeFocused()

      reconnect()
      await demo.at.tryAgain('Due Date').click()

      await expect(demo.at.dateSegment('Due Date', 'month')).toBeFocused()
      await page.keyboard.press('Escape')
      await expect(demo.at.control('Due Date')).toHaveCount(0)
    })
  })
}

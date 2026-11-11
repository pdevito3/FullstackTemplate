import { test, expect } from '@tests/support/fixtures'
import { filterBuilderDemoPage } from './filter-builder.tasks'

// The date control loads its code when it first shows. Match its source file only.
const dateControlSource = /\/components\/filter-builder\/controls\/date-filter\.tsx/

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
  })
}

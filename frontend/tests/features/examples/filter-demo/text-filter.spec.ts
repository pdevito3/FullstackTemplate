import { test, expect } from '@tests/support/fixtures'
import { filterDemoPage } from './filter-demo.tasks'

test.describe('a text filter with a quote', () => {
  test('writes the value as a raw QueryKit literal, so that the Server reads the quote as text', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', 'Say "hi" now')

    await expect(demo.at.queryKit).toHaveText('firstName @=* """Say "hi" now"""')
  })
})

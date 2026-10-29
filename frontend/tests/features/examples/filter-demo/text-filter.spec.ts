import { test, expect } from '@tests/support/fixtures'
import { filterDemoPage } from './filter-demo.tasks'

test.describe('a text filter with a quote', () => {
  test('writes the value as a raw QueryKit literal, so that the Server reads the quote as text', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', 'Say "hi" now')

    await expect(demo.at.queryKit).toHaveText('firstName @=* """Say "hi" now"""')
  })

  test('refuses a value that starts or ends with a quote, because QueryKit cannot hold that quote', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.chooseProperty('First Name')

    await demo.at.textValue('First Name').fill('"Paul"')

    await expect(demo.at.textValue('First Name')).toHaveAccessibleDescription(
      'A filter value cannot start or end with a quote. Remove the quote, or choose Contains and type the text inside the quotes.',
    )
    await expect(demo.at.addFilter('First Name')).toBeDisabled()
  })

  test('refuses a value of only quotes and tells the user why', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.chooseProperty('First Name')

    await demo.at.textValue('First Name').fill('""')

    await expect(demo.at.textValue('First Name')).toHaveAccessibleDescription(/cannot start or end with a quote/)
    await expect(demo.at.addFilter('First Name')).toBeDisabled()
  })
})

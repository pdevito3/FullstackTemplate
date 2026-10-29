import { test, expect } from '@tests/support/fixtures'
import { filterDemoPage } from './filter-demo.tasks'

test.describe('a text filter with a quote', () => {
  test('writes the value as a raw QueryKit literal, so that the Server reads the quote as text', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', 'Say "hi" now')

    await expect(demo.at.queryKit).toHaveText('firstName @=* """Say "hi" now"""')
  })

  test('keeps the text between the quotes at the ends, because QueryKit cannot keep those quotes', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', '"Paul"')

    await expect(demo.at.editFilter('First Name')).toHaveAccessibleName('First Name | Contains | Paul')
    await expect(demo.at.queryKit).toHaveText('firstName @=* "Paul"')
  })

  test('cannot add a value of only quotes', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.chooseProperty('First Name')

    await demo.at.textValue('First Name').fill('""')

    await expect(demo.at.addFilter('First Name')).toBeDisabled()
  })
})

import { test, expect } from '@tests/support/fixtures'
import { filterDemoPage } from './filter-demo.tasks'

test.describe('a text filter with a quote', () => {
  test('writes the value as a padded raw QueryKit literal, so that the Server reads the quote as text', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', 'Say "hi" now')

    await expect(demo.at.queryKit).toHaveText('firstName @=* """ Say "hi" now """')
  })

  test('keeps a quote at the start and the end of the value', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', '"Paul"')

    await expect(demo.at.queryKit).toHaveText('firstName @=* """ "Paul" """')
  })
})

test.describe('a text filter in brackets', () => {
  test('takes a whole value in brackets', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', '[DRAFT]')

    await expect(demo.at.queryKit).toHaveText('firstName @=* "[DRAFT]"')
  })

  test('takes brackets inside the value', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', '[DRAFT] plan')

    await expect(demo.at.queryKit).toHaveText('firstName @=* "[DRAFT] plan"')
  })
})

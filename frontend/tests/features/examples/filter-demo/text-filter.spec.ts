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
    await demo.at.addFilter('First Name').click()

    await expect(demo.at.textValue('First Name')).toHaveAccessibleDescription(
      'A filter value cannot start or end with a quote. Remove the quote at the start or the end.',
    )
    await expect(demo.at.control('First Name')).toBeVisible()
    await expect(demo.at.queryKit).toBeHidden()
  })

  test('refuses a value of only quotes and tells the user why', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.chooseProperty('First Name')

    await demo.at.textValue('First Name').fill('""')
    await demo.at.addFilter('First Name').click()

    await expect(demo.at.textValue('First Name')).toHaveAccessibleDescription(/cannot start or end with a quote/)
    await expect(demo.at.control('First Name')).toBeVisible()
  })

  test('closes the dialog with one mouse click on Cancel after a quote at the end', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.chooseProperty('First Name')
    await demo.at.textValue('First Name').fill('Paul"')

    await demo.at.button('First Name', 'Cancel').click()

    await expect(demo.at.control('First Name')).toBeHidden()
  })

  test('takes one mouse click on Case sensitive after a quote at the end', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.chooseProperty('First Name')
    await demo.at.textValue('First Name').fill('Paul"')

    await demo.select(demo.at.caseSensitive('First Name'))

    await expect(demo.at.caseSensitive('First Name')).toBeChecked()
  })
})

test.describe('a text filter in brackets', () => {
  test('refuses a whole value in brackets, because QueryKit reads it as a list', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.chooseProperty('First Name')

    await demo.at.textValue('First Name').fill('[DRAFT]')
    await demo.at.addFilter('First Name').click()

    await expect(demo.at.textValue('First Name')).toHaveAccessibleDescription(
      'A filter value cannot start with [ and end with ]. Remove the [ at the start or the ] at the end.',
    )
    await expect(demo.at.queryKit).toBeHidden()
  })

  test('takes brackets inside the value', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.addTextFilter('First Name', '[DRAFT] plan')

    await expect(demo.at.queryKit).toHaveText('firstName @=* "[DRAFT] plan"')
  })
})

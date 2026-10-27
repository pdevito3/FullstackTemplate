import type { Locator, Page } from '@playwright/test'
import { test, expect } from '@tests/support/fixtures'
import { filterDemoPage } from '../filter-demo/filter-demo.tasks'

const out = process.env.SHOTS_DIR!
const fixedDay = new Date('2026-08-15T12:00:00Z')

const sizes = { desktop: { width: 1280, height: 900 }, phone: { width: 320, height: 800 } } as const
const schemes = ['light', 'dark'] as const

// The sheet and the popovers slide in. Wait until the box holds still, so two runs of the same code give the same pixels.
async function settle(locator: Locator) {
  let last = ''
  for (let stable = 0; stable < 5; ) {
    await locator.page().waitForTimeout(100)
    const box = JSON.stringify(await locator.boundingBox())
    stable = box === last ? stable + 1 : 0
    last = box
  }
}

async function shot(locator: Locator, name: string) {
  await locator.page().mouse.move(0, 0)
  await settle(locator)
  await locator.screenshot({ path: `${out}/${name}.png`, animations: 'disabled', caret: 'hide' })
}

async function openCreatedDate(page: Page) {
  const demo = filterDemoPage(page)
  await demo.openOn(fixedDay)
  await demo.chooseProperty('Created Date')
  return demo
}

for (const [sizeName, viewport] of Object.entries(sizes)) {
  for (const colorScheme of schemes) {
    test.describe(`${sizeName} ${colorScheme}`, () => {
      test.use({ viewport, colorScheme })
      const tag = `${sizeName}-${colorScheme}`

      test('filter date control', async ({ page }) => {
        const demo = await openCreatedDate(page)
        const control = demo.at.control('Created Date')
        await shot(control, `${tag}-filter-single`)

        await demo.openMonthPicker('Created Date')
        await shot(control, `${tag}-filter-months`)
        await control.getByRole('button', { name: 'Select year', exact: true }).click()
        await expect(control.getByRole('listbox', { name: 'Year picker' })).toBeVisible()
        await shot(control, `${tag}-filter-years`)
      })

      test('filter date control between', async ({ page }) => {
        const demo = await openCreatedDate(page)
        const control = demo.at.control('Created Date')
        await demo.chooseBetween('Created Date')
        await shot(control, `${tag}-filter-between`)
        await demo.typeEndBeforeStart('Created Date')
        await shot(control, `${tag}-filter-between-error`)
        await demo.typeShortYear('Created Date', 'Start date')
        await shot(control, `${tag}-filter-between-short-year`)
      })

      test('components pickers', async ({ page }) => {
        await page.clock.setFixedTime(fixedDay)
        await page.goto('/components')
        await expect(page.getByRole('heading', { name: 'Component Library' })).toBeVisible()

        const startDate = page.getByRole('group', { name: 'Start date', exact: true })
        await startDate.getByRole('button').click()
        const pickerDialog = page.getByRole('dialog').last()
        await expect(pickerDialog.getByRole('grid')).toBeVisible()
        await shot(pickerDialog, `${tag}-date-picker-popover`)
        await page.keyboard.press('Escape')
        await expect(pickerDialog).toBeHidden()

        await page.getByRole('button', { name: 'Pick a date', exact: true }).click()
        const pickADate = page.getByRole('dialog', { name: 'Pick a date' })
        await expect(pickADate.getByRole('grid')).toBeVisible()
        await shot(pickADate, `${tag}-pick-a-date`)
        await pickADate.getByRole('button', { name: 'Select month', exact: true }).click()
        await expect(pickADate.getByRole('listbox', { name: 'Month picker' })).toBeVisible()
        await shot(pickADate, `${tag}-pick-a-date-months`)
      })
    })
  }
}

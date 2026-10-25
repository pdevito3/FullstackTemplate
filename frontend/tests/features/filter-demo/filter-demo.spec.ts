import { test, expect } from '@tests/support/fixtures'
import { takesUpSpace } from '@tests/support/screen'
import { filterDemoPage } from './filter-demo.tasks'

const phone = { width: 390, height: 844 }
const dayInASixWeekMonth = new Date('2026-08-15T12:00:00Z')

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

  test('names the Is Blocked control only in the title of the sheet', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Is Blocked')

    expect(await takesUpSpace(demo.at.controlLabel('Is Blocked'))).toBe(false)
    await expect(demo.at.valueGroup('Is Blocked')).toBeVisible()
  })

  test('adds a date filter from its control', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Created Date')
    await demo.addTodayFilter('Created Date')

    await expect(demo.at.removeFilter('Created Date')).toBeVisible()
    await expect(demo.at.control('Created Date')).toBeHidden()
  })
})

for (const viewport of [
  { width: 320, height: 780 },
  { width: 360, height: 780 },
  { width: 390, height: 844 },
]) {
  test.describe(`the date control on a ${viewport.width} px phone`, () => {
    test.use({ viewport })

    for (const { mode, quickSelect } of [
      { mode: 'On', quickSelect: 'Yesterday' },
      { mode: 'Between', quickSelect: 'Last Year' },
    ]) {
      test(`fits the ${mode} calendar in the column of the control`, async ({ page }) => {
        const demo = filterDemoPage(page)
        await demo.openOn(dayInASixWeekMonth)

        await demo.chooseProperty('Created Date')
        if (mode === 'Between') await demo.chooseBetween('Created Date')

        expect(await demo.sidewaysScrollOf('Created Date')).toBe(0)
        await expect(demo.at.calendar('Created Date')).toBeInViewport({ ratio: 1 })
        await expect(demo.at.button('Created Date', quickSelect)).toBeInViewport({ ratio: 1 })
        const calendar = await demo.edgesOf(demo.at.calendar('Created Date'))
        const firstMode = await demo.edgesOf(demo.at.button('Created Date', 'On'))
        const lastMode = await demo.edgesOf(demo.at.button('Created Date', 'Between'))
        expect(calendar.left).toBeCloseTo(firstMode.left, 0)
        expect(calendar.right).toBeCloseTo(lastMode.right, 0)
        if (mode === 'Between') {
          const endDate = await demo.edgesOf(demo.at.dateField('Created Date', 'End date'))
          expect(endDate.right).toBeCloseTo(lastMode.right, 0)
        }
      })
    }
  })
}

test.describe('the date control in the dialog of a 768 px tablet', () => {
  test.use({ viewport: { width: 768, height: 1024 } })

  test('fits the Between calendar in the column of the control', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    const startDate = await demo.edgesOf(demo.at.dateField('Created Date', 'Start date'))
    const calendar = await demo.edgesOf(demo.at.calendar('Created Date'))
    const firstMode = await demo.edgesOf(demo.at.button('Created Date', 'On'))
    const lastMode = await demo.edgesOf(demo.at.button('Created Date', 'Between'))
    expect(startDate.left).toBeCloseTo(firstMode.left, 0)
    expect(calendar.right).toBeCloseTo(lastMode.right, 0)
  })
})

// The date control is the widest. A submenu with it needs about 911 px beside the sidebar.
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

  test('names the Is Blocked control in the menu', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Is Blocked')

    expect(await takesUpSpace(demo.at.controlLabel('Is Blocked'))).toBe(true)
    await expect(demo.at.valueGroup('Is Blocked')).toBeVisible()
  })

  test('shows the Created Date control beside the open menu', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Created Date')

    await expect(demo.at.addFilter('Created Date')).toBeInViewport({ ratio: 1 })
    await expect(demo.at.searchProperties).toBeVisible()
  })
})

const shortWindow = { width: 1280, height: 512 }

test.describe('the Add Filter menu on a short desktop window', () => {
  test.use({ viewport: shortWindow })

  test('keeps a tall control on the screen and scrolls it to its Add Filter button', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Scheduled At (Local)')

    expect(await demo.visibleBottomOf('Scheduled At (Local)')).toBeLessThanOrEqual(shortWindow.height)
    await demo.scrollControlToEnd('Scheduled At (Local)')
    await expect(demo.at.addFilter('Scheduled At (Local)')).toBeInViewport({ ratio: 1 })
  })
})

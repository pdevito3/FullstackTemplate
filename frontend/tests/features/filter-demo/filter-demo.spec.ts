import { test, expect } from '@tests/support/fixtures'
import { takesUpSpace } from '@tests/support/screen'
import { filterDemoPage } from './filter-demo.tasks'

const phone = { width: 390, height: 844 }
const dayInASixWeekMonth = new Date('2026-08-15T12:00:00Z')
const dayInAFiveWeekMonth = new Date('2026-09-15T12:00:00Z')

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

  test('gives each control the same 16 px side margins in the sheet', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    const margins: Record<string, { left: number; right: number }> = {}

    for (const property of ['First Name', 'Status', 'Created Date', 'Comment Count', 'Is Blocked']) {
      await demo.chooseProperty(property)
      margins[property] = await demo.sideMarginsOf(property)
      await demo.closeControl(property)
    }

    expect(margins).toEqual({
      'First Name': { left: 16, right: 16 },
      Status: { left: 16, right: 16 },
      'Created Date': { left: 16, right: 16 },
      'Comment Count': { left: 16, right: 16 },
      'Is Blocked': { left: 16, right: 16 },
    })
  })

  test('names the Is Blocked control only in the title of the sheet', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Is Blocked')

    expect(await takesUpSpace(demo.at.controlLabel('Is Blocked'))).toBe(false)
    await expect(demo.at.valueGroup('Is Blocked')).toBeVisible()
  })

  test('gives the close button of the sheet a 32 px touch target', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Is Blocked')

    expect(await demo.at.button('Is Blocked', 'Close').boundingBox()).toMatchObject({ width: 32, height: 32 })
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

const shortPhone = { width: 320, height: 780 }

test.describe('the Between calendar on a 320 x 780 phone', () => {
  test.use({ viewport: shortPhone })

  test('keeps Add Filter on the screen in a five-week month and in the month picker', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInAFiveWeekMonth)

    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    const addFilter = await demo.edgesOf(demo.at.addFilter('Created Date'))
    expect(Math.abs(addFilter.bottom - shortPhone.height), 'Add Filter sits flush with the bottom edge of the screen').toBeLessThanOrEqual(1)

    await demo.openMonthPicker('Created Date')

    await expect(demo.at.addFilter('Created Date')).toBeInViewport({ ratio: 1 })
  })
})

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

  test('keeps the 28 px close button in the filter dialog', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.addBooleanFilter('Is Blocked', 'True')

    await demo.editFilter('Is Blocked')

    expect(await demo.at.button('Is Blocked', 'Close').boundingBox()).toMatchObject({ width: 28, height: 28 })
  })

  test('shows the True and False buttons of the menu as plain buttons', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Is Blocked')

    await expect(demo.at.button('Is Blocked', 'True')).not.toHaveAttribute('aria-pressed')
    await expect(demo.at.button('Is Blocked', 'False')).not.toHaveAttribute('aria-pressed')
  })

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

  test('keeps the Add Filter button of the date control on the screen after the month picker', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')

    await demo.openMonthPicker('Created Date')
    await demo.pickMonth('Created Date', 'Aug')

    await expect(demo.at.addFilter('Created Date')).toBeInViewport({ ratio: 1 })
  })

  test('edits a date filter in its dialog', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.addTodayFilter('Created Date')

    await demo.editFilter('Created Date')
    await demo.updateWithQuickSelect('Created Date', 'Yesterday')

    await expect(demo.at.editFilter('Created Date')).toHaveAccessibleName(/Aug 14, 2026/)
  })
})

test.describe('the Add Filter menu on a 900 px tall desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } })

  test('keeps the Add Filter button of the date control on the screen after a five-week month', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    await demo.showNextMonth('Created Date')
    await demo.showPreviousMonth('Created Date')

    await expect(demo.at.addFilter('Created Date')).toBeInViewport({ ratio: 1 })
  })
})

test.describe('typed dates in the Between control on a desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } })

  test('keeps Add Filter off after a typed start date is cleared and an end date is typed', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    await demo.typeDate('Created Date', 'Start date', '03102026')
    await demo.clearDate('Created Date', 'Start date')
    await demo.typeDate('Created Date', 'End date', '03202026')

    await expect(demo.at.dateField('Created Date', 'Start date')).toHaveText(/^mm.*dd.*yyyy$/)
    await expect(demo.at.selectMonth('Created Date')).toHaveText('March 2026')
    await expect(demo.at.addFilter('Created Date')).toBeDisabled()
  })

  test('turns Add Filter off when the start date of a set range is cleared', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')
    await demo.at.button('Created Date', 'Last Week').click()

    await demo.clearDate('Created Date', 'Start date')

    await expect(demo.at.addFilter('Created Date')).toBeDisabled()
  })
})

test.describe('a short year in the date control on a desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } })

  for (const mode of ['On', 'Before', 'After']) {
    test(`marks a short year in the ${mode} date after the focus leaves it`, async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.openOn(dayInASixWeekMonth)
      await demo.chooseProperty('Created Date')
      await demo.at.button('Created Date', mode).click()

      await demo.typeDate('Created Date', 'Date', '0310999')
      await demo.leaveDateField()

      const year = demo.at.dateSegment('Created Date', 'Date', 'year')
      await expect(year).toHaveAttribute('aria-invalid', 'true')
      await expect(year).toHaveAccessibleDescription(/Enter a 4-digit year\./)
      await expect(demo.at.addFilter('Created Date')).toBeDisabled()

      await demo.typeDate('Created Date', 'Date', '03102026')

      await expect(year).not.toHaveAttribute('aria-invalid')
      await expect(demo.at.dateMessage('Created Date', 'Enter a 4-digit year.')).toHaveCount(0)
      await expect(demo.at.addFilter('Created Date')).toBeEnabled()
    })
  }

  test('takes a quick select that is pressed right after a short year', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')

    await demo.typeDate('Created Date', 'Date', '0310999')
    await demo.at.button('Created Date', 'Yesterday').click()

    await expect(demo.at.dateField('Created Date', 'Date')).toHaveText('8/14/2026')
    await expect(demo.at.addFilter('Created Date')).toBeEnabled()
  })

  test('marks a short year after a press on a calendar button that does not set the date', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')

    await demo.typeDate('Created Date', 'Date', '0310999')
    await demo.showNextMonth('Created Date')

    await expect(demo.at.dateSegment('Created Date', 'Date', 'year')).toHaveAccessibleDescription(/Enter a 4-digit year\./)
  })

  test('marks a short start year and a short end year in the Between dates', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    await demo.typeDate('Created Date', 'Start date', '0310999')
    await demo.leaveDateField()
    await demo.typeDate('Created Date', 'End date', '0320202612')
    await demo.leaveDateField()

    for (const field of ['Start date', 'End date']) {
      const year = demo.at.dateSegment('Created Date', field, 'year')
      await expect(year).toHaveAttribute('aria-invalid', 'true')
      await expect(year).toHaveAccessibleDescription(/Enter a 4-digit year\./)
    }

    await demo.typeDate('Created Date', 'Start date', '03102026')
    await demo.typeDate('Created Date', 'End date', '03202026')

    await expect(demo.at.dateSegment('Created Date', 'Start date', 'year')).not.toHaveAttribute('aria-invalid')
    await expect(demo.at.dateSegment('Created Date', 'End date', 'year')).not.toHaveAttribute('aria-invalid')
    await expect(demo.at.dateMessage('Created Date', 'Enter a 4-digit year.')).toHaveCount(0)
    await expect(demo.at.addFilter('Created Date')).toBeEnabled()
  })
})

test.describe('the edit dialog of a boolean filter', () => {
  for (const [value, other] of [['True', 'False'], ['False', 'True']] as const) {
    test(`shows ${value} as the current value`, async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.open()
      await demo.addBooleanFilter('Is Blocked', value)

      await demo.editFilter('Is Blocked')

      await expect(demo.at.pressedButtons('Is Blocked')).toHaveText([value])
      await expect(demo.at.button('Is Blocked', other)).toHaveAttribute('aria-pressed', 'false')
    })
  }
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

import { test, expect } from '@tests/support/fixtures'
import { takesUpSpace, useDefaultFontSize } from '@tests/support/screen'
import { filterDemoPage } from './filter-demo.tasks'

const phone = { width: 390, height: 844 }
const dayInASixWeekMonth = '2026-08-15'
const dayInAFiveWeekMonth = '2026-09-15'

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

  test('gives each control the same 16 px space below the title and above the end of the sheet', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    const margins: Record<string, { top: number; bottom: number }> = {}

    for (const property of ['First Name', 'Status', 'Created Date', 'Comment Count', 'Is Blocked']) {
      await demo.chooseProperty(property)
      margins[property] = await demo.verticalMarginsOf(property)
      await demo.closeControl(property)
    }

    expect(margins).toEqual({
      'First Name': { top: 16, bottom: 16 },
      Status: { top: 16, bottom: 16 },
      'Created Date': { top: 16, bottom: 16 },
      'Comment Count': { top: 16, bottom: 16 },
      'Is Blocked': { top: 16, bottom: 16 },
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

  for (const { weeks, today } of [
    { weeks: 'six', today: dayInASixWeekMonth },
    { weeks: 'five', today: dayInAFiveWeekMonth },
  ]) {
    test(`keeps Add Filter on the screen, with the bottom margin of the sheet, in a ${weeks}-week month`, async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.openOn(today)

      await demo.chooseProperty('Created Date')
      await demo.chooseBetween('Created Date')

      await expect(demo.at.addFilter('Created Date')).toBeInViewport({ ratio: 1 })
      const addFilter = await demo.edgesOf(demo.at.addFilter('Created Date'))
      expect(shortPhone.height - addFilter.bottom).toBe(16)
    })
  }

  test('keeps Add Filter on the screen in the month picker', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    await demo.openMonthPicker('Created Date')

    await expect(demo.at.addFilter('Created Date')).toBeInViewport({ ratio: 1 })
  })

  test('shows the line above Add Filter only while the calendar scrolls under it', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    expect(await demo.lineAboveFooterOf(demo.at.addFilter('Created Date'))).toBe(true)

    await demo.scrollControlToEnd('Created Date')

    await expect.poll(() => demo.lineAboveFooterOf(demo.at.addFilter('Created Date'))).toBe(false)
  })
})

test.describe('the Add Filter menu on a phone on its side', () => {
  test.use({ viewport: { width: 667, height: 375 } })

  for (const { property, submit } of [
    { property: 'First Name', submit: 'addFilter' },
    { property: 'Status', submit: 'addSelected' },
    { property: 'Created Date', submit: 'addFilter' },
    { property: 'Comment Count', submit: 'addFilter' },
  ] as const) {
    test(`keeps the submit button of the ${property} control on the screen`, async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.openOn(dayInASixWeekMonth)

      await demo.chooseProperty(property)

      await expect(demo.at[submit](property)).toBeInViewport({ ratio: 1 })
    })

    test(`spans the footer of the ${property} control across the sheet`, async ({ page }) => {
      const demo = filterDemoPage(page)
      await demo.open()

      await demo.chooseProperty(property)

      const sheet = await demo.edgesOf(demo.at.control(property))
      expect(await demo.footerSidesOf(demo.at[submit](property))).toEqual({ left: sheet.left, right: sheet.right })
    })
  }
})

test.describe('the Status control on a touch screen', () => {
  test.use({ viewport: phone, hasTouch: true })

  test('shows the Only button of each option, because a touch screen has no hover', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Status')

    await expect(demo.at.onlyButtons('Status')).toHaveCount(5)
    for (const only of await demo.at.onlyButtons('Status').all()) await expect(only).toHaveCSS('opacity', '1')
  })
})

const toastsWithDescriptions = [
  { message: 'Something went wrong', description: 'The server did not answer. Try again.' },
  { message: 'Changes saved', description: 'The filter list is up to date.' },
]

test.describe('the filter dialog on a 900 x 800 screen', () => {
  test.use({ viewport: { width: 900, height: 800 } })

  test('keeps two toasts off the Add Filter button', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')

    await demo.showToasts(...toastsWithDescriptions)

    for (const toast of await demo.at.toasts.all()) {
      expect(await demo.overlapOf(demo.at.addFilter('Created Date'), toast)).toBeNull()
    }
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

test.describe('a date message in the date control on a 900 px tall desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } })

  const shortYear = 'Enter a 4-digit year.'
  const endBeforeStart = 'The end date is before the start date.'

  test('gives a date field with a short year the red border of its message', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')

    await demo.typeShortYear('Created Date', 'Date')

    const red = await demo.textColorOf(demo.at.dateMessage('Created Date', shortYear))
    await expect(demo.at.dateField('Created Date', 'Date')).toHaveCSS('border-top-color', red)
  })

  test('gives an end date before the start date the red border of its message', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    await demo.typeEndBeforeStart('Created Date')

    const red = await demo.textColorOf(demo.at.dateMessage('Created Date', endBeforeStart))
    await expect(demo.at.dateField('Created Date', 'End date')).toHaveCSS('border-top-color', red)
  })

  test('keeps Add Filter on the screen when a short year shows its message', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')

    await demo.typeShortYear('Created Date', 'Date')

    await expect(demo.at.addFilter('Created Date')).toBeInViewport({ ratio: 1 })
  })

  test('keeps Add Filter on the screen when an end date before the start date shows its message', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.chooseBetween('Created Date')

    await demo.typeEndBeforeStart('Created Date')

    await expect(demo.at.addFilter('Created Date')).toBeInViewport({ ratio: 1 })
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

  test('keeps the Update Filter button of the edit dialog of a date filter on the screen', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openOn(dayInASixWeekMonth)
    await demo.chooseProperty('Created Date')
    await demo.addTodayFilter('Created Date')

    await demo.editFilter('Created Date')

    await expect(demo.at.button('Created Date', 'Update Filter')).toBeInViewport({ ratio: 1 })
  })

  test('keeps a tall control on the screen and scrolls it to its Add Filter button', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.chooseProperty('Scheduled At (Local)')

    expect(await demo.visibleBottomOf('Scheduled At (Local)')).toBeLessThanOrEqual(shortWindow.height)
    await demo.scrollControlToEnd('Scheduled At (Local)')
    await expect(demo.at.addFilter('Scheduled At (Local)')).toBeInViewport({ ratio: 1 })
  })
})

test.describe('a date filter of a place in another zone', () => {
  test('takes today and reads the typed time in the zone of the place, not of the browser', async ({ page }) => {
    const demo = filterDemoPage(page)
    // 07:00 UTC on August 16 is 20:00 on August 15 in Pago Pago. In UTC and in Kiritimati, it is August 16.
    await demo.openAt(new Date('2026-08-16T07:00:00Z'))
    await demo.chooseProperty('Starts At in Pago Pago')

    await demo.addTodayAtTimeFilter('Starts At in Pago Pago', 'Time in Pacific/Pago Pago', '09:00')

    await expect(demo.at.editFilter('Starts At in Pago Pago')).toHaveAccessibleName(/Aug 15, 2026/)
    await expect(demo.at.queryKit).toHaveText('startsAt == "2026-08-15T20:00:00Z"')
  })
})

test.describe('a date filter with no zone, in a browser in New York', () => {
  test.use({ timezoneId: 'America/New_York' })
  test.skip(() => test.info().project.name !== 'utc', 'The describe sets its own zone.')

  test('takes a local time that the clocks of the browser skip, because the field has no zone', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openAt(new Date('2026-03-01T17:00:00Z'))
    await demo.chooseProperty('Scheduled At (Local)')

    await demo.typeDate('Scheduled At (Local)', 'Date', '03082026')
    await demo.at.time('Scheduled At (Local)', 'Time').fill('02:30')
    await demo.at.addFilter('Scheduled At (Local)').click()

    await expect(demo.at.queryKit).toHaveText('scheduledAt == "2026-03-08T02:30:00"')
  })

  test('names no zone when it refuses a time of the browser that the clocks skip', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openAt(new Date('2026-03-01T17:00:00Z'))
    await demo.chooseProperty('Published At (UTC)')

    await demo.typeDate('Published At (UTC)', 'Date', '03082026')
    await demo.at.time('Published At (UTC)', 'Time').fill('02:30')

    await expect(demo.at.dateMessage('Published At (UTC)', 'This time does not exist on that day, because the clocks go forward. Choose another time.')).toBeVisible()
    await expect(demo.at.addFilter('Published At (UTC)')).toBeDisabled()
  })
})

test.describe('the filter builder after the focused button goes away', () => {
  test('moves the focus to Add Filter when the user removes a filter with the keyboard', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.toolbarButton('Urgent Bugs').click()

    await demo.at.removeFilterNamed('Labels | Has | Bug').focus()
    await page.keyboard.press('Enter')

    await expect(demo.at.removeFilter('Labels')).toHaveCount(1)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })

  test('moves the focus to Add Filter after Clear', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.toolbarButton('Urgent Bugs').click()

    await demo.at.toolbarButton('Clear').focus()
    await page.keyboard.press('Enter')

    await expect(demo.at.toolbarButton('Clear')).toHaveCount(0)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })

  test('moves the focus to Cancel when the user starts to group, and back to Add Filter after Cancel', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.toolbarButton('Urgent Bugs').click()

    await demo.at.toolbarButton('Group Filters').focus()
    await page.keyboard.press('Enter')
    await expect(demo.at.toolbarButton('Cancel')).toBeFocused()

    await page.keyboard.press('Enter')
    await expect(demo.at.toolbarButton('Cancel')).toHaveCount(0)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })

  test('moves the focus to Add Filter after Group Selected and after Ungroup', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.toolbarButton('Urgent Bugs').click()
    await demo.at.toolbarButton('Group Filters').click()
    for (const value of ['Bug', 'Urgent']) {
      await demo.select(demo.at.selectFilter(`Labels | Has | ${value}`))
    }

    await demo.at.groupSelected.focus()
    await page.keyboard.press('Enter')
    await expect(demo.at.addFilterMenu).toBeFocused()

    await demo.at.toolbarButton('Ungroup').focus()
    await page.keyboard.press('Enter')
    await expect(demo.at.toolbarButton('Ungroup')).toHaveCount(0)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })
})

test.describe('the filter builder after a click removes the clicked button', () => {
  test('moves the focus to Add Filter after a click on Remove filter', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.toolbarButton('Urgent Bugs').click()

    await demo.at.removeFilterNamed('Labels | Has | Bug').click()

    await expect(demo.at.removeFilter('Labels')).toHaveCount(1)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })

  test('moves the focus to Add Filter after a click on Clear', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.toolbarButton('Urgent Bugs').click()

    await demo.at.toolbarButton('Clear').click()

    await expect(demo.at.toolbarButton('Clear')).toHaveCount(0)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })

  test('moves the focus to Add Filter after a click on Delete group', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.toolbarButton('Complex Nested Query').click()

    await demo.at.groupAction(1, 'Delete group').click()

    await expect(demo.at.group(1)).toHaveCount(0)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })
})

test.describe('the filter builder after a key removes a button in a nested group', () => {
  test('moves the focus to Add Filter after Remove filter in the inner group', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openNestedGroups()

    await demo.at.removeFilterNamed('First Name | Equals | Jane').focus()
    await page.keyboard.press('Enter')

    await expect(demo.at.removeFilterNamed('First Name | Equals | Jane')).toHaveCount(0)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })

  test('moves the focus to Add Filter after Delete group of the inner group', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openNestedGroups()

    await demo.at.groupAction(2, 'Delete group').focus()
    await page.keyboard.press('Enter')

    await expect(demo.at.group(2)).toHaveCount(0)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })

  test('moves the focus to Add Filter after Ungroup of the inner group', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openNestedGroups()

    await demo.at.groupAction(2, 'Ungroup').focus()
    await page.keyboard.press('Enter')

    await expect(demo.at.group(2)).toHaveCount(0)
    await expect(demo.at.addFilterMenu).toBeFocused()
  })
})

test.describe('a filter group on a 390 px phone', () => {
  test.use({ viewport: { width: 390, height: 844 } })

  test('keeps Ungroup and Delete group on the line of its Group badge', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()

    await demo.at.toolbarButton('Complex Nested Query').click()

    const badge = await demo.edgesOf(demo.at.groupBadge(1))
    for (const action of ['Ungroup', 'Delete group'] as const) {
      const button = await demo.edgesOf(demo.at.groupAction(1, action))
      expect(button.top, action).toBeLessThan(badge.bottom)
      expect(button.bottom, action).toBeGreaterThan(badge.top)
    }
  })
})

test.describe('a nested filter group on a 320 px phone with a 24 px font', () => {
  test.use({ viewport: { width: 320, height: 640 } })

  test('fits the group header and the filters inside each group', async ({ page }) => {
    const demo = filterDemoPage(page)
    await useDefaultFontSize(page, 24)
    await demo.openNestedGroups()

    await demo.at.toolbarButton('Group Filters').click()

    for (const depth of [1, 2]) {
      for (const action of ['Ungroup', 'Delete group'] as const) {
        expect(await demo.overlapOf(demo.at.groupBadge(depth), demo.at.groupAction(depth, action)), `depth ${depth} ${action}`).toBeNull()
      }
      expect(await demo.overflowOf(demo.at.group(depth)), `depth ${depth}`).toBeLessThanOrEqual(0)
      const header = await demo.edgesOf(demo.at.groupHeader(depth))
      expect((await demo.edgesOf(demo.at.groupBadge(depth))).right, `depth ${depth} badge`).toBeLessThanOrEqual(header.right)
      expect(await demo.onOneLine(demo.at.groupAction(depth, 'Ungroup'), demo.at.groupAction(depth, 'Delete group')), `depth ${depth} actions`).toBe(true)
      expect(await demo.onOneLine(demo.at.groupCheckboxLabel(depth), demo.at.groupBadge(depth)), `depth ${depth} checkbox`).toBe(true)
    }
  })
})

test.describe('a filter group with one short filter on a desktop', () => {
  test.use({ viewport: { width: 1280, height: 800 } })

  test('keeps its header on one line', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.open()
    await demo.at.toolbarButton('Urgent Bugs').click()
    await demo.at.toolbarButton('Group Filters').click()
    for (const value of ['Bug', 'Urgent']) {
      await demo.select(demo.at.selectFilter(`Labels | Has | ${value}`))
    }
    await demo.at.groupSelected.click()

    await demo.at.removeFilterNamed('Labels | Has | Urgent').click()

    await expect(demo.at.group(1)).toBeVisible()
    expect(await demo.onOneLine(demo.at.groupBadge(1), demo.at.groupAction(1, 'Delete group'))).toBe(true)
    expect(await demo.onOneLine(demo.at.groupBadge(1), demo.at.groupAction(1, 'Ungroup'))).toBe(true)
  })
})

test.describe('a nested filter group on a 320 px phone', () => {
  test.use({ viewport: { width: 320, height: 640 } })

  test('keeps the checkbox of each group on the line of its badge', async ({ page }) => {
    const demo = filterDemoPage(page)
    await demo.openNestedGroups()

    await demo.at.toolbarButton('Group Filters').click()

    for (const depth of [1, 2]) {
      expect(await demo.onOneLine(demo.at.groupCheckboxLabel(depth), demo.at.groupBadge(depth)), `depth ${depth}`).toBe(true)
      expect(await demo.onOneLine(demo.at.groupAction(depth, 'Ungroup'), demo.at.groupAction(depth, 'Delete group')), `depth ${depth} actions`).toBe(true)
    }
  })
})

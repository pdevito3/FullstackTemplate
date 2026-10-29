import { expect, type Locator, type Page } from '@playwright/test'
import type { Notification as AppNotification } from '@/components/notification'
import { browserZone, instantIn } from '@tests/support/clock'
import { filterDemo } from './filter-demo.locators'

interface ToastText {
  message: string
  description: string
}

export function filterDemoPage(page: Page) {
  const at = filterDemo(page)

  const showOtherMonth = async (propertyLabel: string, name: 'Next' | 'Previous') => {
    const shownMonth = await at.selectMonth(propertyLabel).textContent()
    await at.calendarNav(propertyLabel, name).click()
    await expect(at.selectMonth(propertyLabel)).not.toHaveText(shownMonth ?? '')
  }

  return {
    at,

    async open() {
      await page.goto('/filter-demo')
      await expect(at.heading).toBeVisible()
    },

    /** Opens the page at noon of `today`, a date such as `2026-08-15`, in the zone of the browser. */
    async openOn(today: string) {
      await page.clock.setFixedTime(instantIn(`${today}T12:00`, browserZone()))
      await page.goto('/filter-demo')
      await expect(at.heading).toBeVisible()
    },

    async openAt(instant: Date) {
      await page.clock.setFixedTime(instant)
      await page.goto('/filter-demo')
      await expect(at.heading).toBeVisible()
    },

    // React Aria hides the native input, so the click goes to its label, as a user clicks.
    async select(checkbox: Locator) {
      await checkbox.locator('xpath=ancestor::label').click()
    },

    /** How far the content of the element goes past its right edge. */
    async overflowOf(element: Locator) {
      return element.evaluate((node) => node.scrollWidth - node.clientWidth)
    },

    async openNestedGroups() {
      await this.open()
      await at.toolbarButton('Complex Nested Query').click()
      await at.toolbarButton('Group Filters').click()
      await this.select(at.groupAction(1, 'Select group'))
      await this.select(at.selectFilter('First Name | Equals | John'))
      await at.groupSelected.click()
      await expect(at.group(2)).toBeVisible()
    },

    async chooseProperty(propertyLabel: string) {
      await at.addFilterMenu.click()
      await at.property(propertyLabel).click()
      await expect(at.control(propertyLabel)).toBeVisible()
      await expect(at.loadingControl).toHaveCount(0)
    },

    /** Shows error toasts, as a failed request does while the control is open. The filter demo sends no request. */
    async showToasts(...toasts: ToastText[]) {
      await page.evaluate(async (toasts) => {
        // The Vite dev server serves each source file at its path.
        const notificationModule: string = '/src/components/notification.ts'
        const { Notification } = (await import(/* @vite-ignore */ notificationModule)) as { Notification: typeof AppNotification }
        for (const { message, description } of toasts) Notification.error(message, { description })
      }, toasts)
      await expect(at.toasts).toHaveCount(toasts.length)
    },

    async closeControl(propertyLabel: string) {
      await at.button(propertyLabel, 'Close').click()
      await expect(at.control(propertyLabel)).toBeHidden()
    },

    async chooseBetween(propertyLabel: string) {
      await at.button(propertyLabel, 'Between').click()
      await expect(at.dateField(propertyLabel, 'Start date')).toBeVisible()
    },

    async showNextMonth(propertyLabel: string) {
      await showOtherMonth(propertyLabel, 'Next')
    },

    async showPreviousMonth(propertyLabel: string) {
      await showOtherMonth(propertyLabel, 'Previous')
    },

    async openMonthPicker(propertyLabel: string) {
      await at.selectMonth(propertyLabel).click()
      await expect(at.monthOption(propertyLabel, 'Jan')).toBeVisible()
    },

    async pickMonth(propertyLabel: string, name: string) {
      await at.monthOption(propertyLabel, name).click()
      await expect(at.selectMonth(propertyLabel)).toContainText(name)
    },

    async typeDate(propertyLabel: string, field: string, digits: string) {
      await at.dateSegment(propertyLabel, field, 'month').click()
      await page.keyboard.type(digits)
    },

    async leaveDateField() {
      await page.keyboard.press('Tab')
    },

    async typeShortYear(propertyLabel: string, field: string) {
      await at.dateSegment(propertyLabel, field, 'month').click()
      await page.keyboard.type('0310999')
      await page.keyboard.press('Tab')
      await expect(at.dateMessage(propertyLabel, 'Enter a 4-digit year.')).toBeVisible()
    },

    async typeEndBeforeStart(propertyLabel: string) {
      await at.dateSegment(propertyLabel, 'Start date', 'month').click()
      await page.keyboard.type('03102026')
      await at.dateSegment(propertyLabel, 'End date', 'month').click()
      await page.keyboard.type('03012026')
      await expect(at.dateMessage(propertyLabel, 'The end date is before the start date.')).toBeVisible()
    },

    async clearDate(propertyLabel: string, field: string) {
      for (const part of ['year', 'day', 'month'] as const) {
        await at.dateSegment(propertyLabel, field, part).click()
        for (let i = 0; i < 4; i++) await page.keyboard.press('Backspace')
      }
      await expect(at.dateField(propertyLabel, field)).toHaveText(/^mm.*dd.*yyyy$/)
    },

    async addBooleanFilter(propertyLabel: string, value: 'True' | 'False') {
      await at.addFilterMenu.click()
      await at.property(propertyLabel).click()
      await at.button(propertyLabel, value).click()
      await expect(at.control(propertyLabel)).toBeHidden()
    },

    async editFilter(propertyLabel: string) {
      await at.editFilter(propertyLabel).click()
      await expect(at.control(propertyLabel)).toBeVisible()
    },

    async updateWithQuickSelect(propertyLabel: string, quickSelect: string) {
      await at.button(propertyLabel, quickSelect).click()
      await at.button(propertyLabel, 'Update Filter').click()
      await expect(at.control(propertyLabel)).toBeHidden()
    },

    async addTodayAtTimeFilter(propertyLabel: string, timeLabel: string, time: string) {
      await at.button(propertyLabel, 'Today').click()
      await at.time(propertyLabel, timeLabel).fill(time)
      await at.addFilter(propertyLabel).click()
      await expect(at.control(propertyLabel)).toBeHidden()
    },

    async addTodayFilter(propertyLabel: string) {
      await at.button(propertyLabel, 'Today').click()
      await at.addFilter(propertyLabel).click()
      await expect(at.control(propertyLabel)).toBeHidden()
    },

    async cssFiltersAround(propertyLabel: string) {
      return at.control(propertyLabel).evaluate((control) => {
        const filters: string[] = []
        for (let element: Element | null = control; element; element = element.parentElement) {
          const { filter } = getComputedStyle(element)
          if (filter !== 'none') filters.push(filter)
        }
        return filters
      })
    },

    async scrollControlToEnd(propertyLabel: string) {
      await at.control(propertyLabel).hover()
      await page.mouse.wheel(0, 200)
    },

    /** The bottom edge of the control as the user sees it: the edge of the element that clips it, or its own. */
    async visibleBottomOf(propertyLabel: string) {
      return at.control(propertyLabel).evaluate((control) => {
        for (let element = control.parentElement; element && element !== document.body; element = element.parentElement) {
          if (getComputedStyle(element).overflowY !== 'visible') return element.getBoundingClientRect().bottom
        }
        return control.getBoundingClientRect().bottom
      })
    },

    /** Whether the sticky footer that holds the button shows a line on its top edge. */
    async lineAboveFooterOf(button: Locator) {
      return button.evaluate((element) => {
        let footer: Element | null = element
        while (footer && getComputedStyle(footer).position !== 'sticky') footer = footer.parentElement
        if (!footer) throw new Error('The button is not in a sticky footer.')
        const { borderTopWidth, borderTopColor } = getComputedStyle(footer)
        // A canvas reads the alpha of any color syntax, such as the oklab() of an animated color.
        const context = document.createElement('canvas').getContext('2d')!
        context.fillStyle = borderTopColor
        context.fillRect(0, 0, 1, 1)
        return parseFloat(borderTopWidth) > 0 && context.getImageData(0, 0, 1, 1).data[3] > 0
      })
    },

    /** The left and right edges of the sticky footer that holds the button. */
    async footerSidesOf(button: Locator) {
      return button.evaluate((element) => {
        let footer: Element | null = element
        while (footer && getComputedStyle(footer).position !== 'sticky') footer = footer.parentElement
        if (!footer) throw new Error('The button is not in a sticky footer.')
        const { left, right } = footer.getBoundingClientRect()
        return { left, right }
      })
    },

    async sidewaysScrollOf(propertyLabel: string) {
      return at.control(propertyLabel).evaluate((control) => {
        let widest = 0
        for (const element of [control, ...control.querySelectorAll('*')]) {
          if (['auto', 'scroll'].includes(getComputedStyle(element).overflowX)) {
            widest = Math.max(widest, element.scrollWidth - element.clientWidth)
          }
        }
        return widest
      })
    },

    async overlapOf(element: Locator, other: Locator) {
      const a = await this.edgesOf(element)
      const b = await this.edgesOf(other)
      const width = Math.min(a.right, b.right) - Math.max(a.left, b.left)
      const height = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top)
      return width > 0 && height > 0 ? { width, height } : null
    },

    /** Whether the vertical middle of each element is inside the other: the two are on one line. */
    async onOneLine(element: Locator, other: Locator) {
      const a = await this.edgesOf(element)
      const b = await this.edgesOf(other)
      const middle = (edges: typeof a) => (edges.top + edges.bottom) / 2
      return middle(a) > b.top && middle(a) < b.bottom && middle(b) > a.top && middle(b) < a.bottom
    },

    async edgesOf(element: Locator) {
      const box = await element.boundingBox()
      if (!box) throw new Error('The element is not on the page.')
      return { left: box.x, right: box.x + box.width, top: box.y, bottom: box.y + box.height }
    },

    /** The space between each side of the sheet and the nearest button or text box of the control, without the Close button. */
    async sideMarginsOf(propertyLabel: string) {
      const sheet = await at.control(propertyLabel).boundingBox()
      const close = await at.button(propertyLabel, 'Close').boundingBox()
      if (!sheet || !close) throw new Error('The sheet is not on the screen.')
      let left = Infinity
      let right = Infinity
      for (const field of await at.fields(propertyLabel).all()) {
        const box = await field.boundingBox()
        if (!box || box.width === 0 || (box.x === close.x && box.y === close.y)) continue
        left = Math.min(left, box.x - sheet.x)
        right = Math.min(right, sheet.x + sheet.width - (box.x + box.width))
      }
      return { left, right }
    },

    async textColorOf(element: Locator) {
      return element.evaluate((node) => getComputedStyle(node).color)
    },

    /**
     * The space from the title of the sheet down to the first thing of the control, and from the last thing of the
     * control down to the end of the sheet. Where the sheet scrolls, the end is the end of its scrolled content.
     */
    async verticalMarginsOf(propertyLabel: string) {
      const sheet = await at.control(propertyLabel).elementHandle()
      return at.controlTitle(propertyLabel).evaluate((title, sheet) => {
        const shows = (element: Element) => {
          const { width, height } = element.getBoundingClientRect()
          return width > 1 && height > 1
        }
        const titleBottom = title.getBoundingClientRect().bottom
        let top = Infinity
        let bottom = -Infinity
        for (const element of sheet!.querySelectorAll('*')) {
          const box = element.getBoundingClientRect()
          const isLeaf = ['BUTTON', 'INPUT'].includes(element.tagName) || ![...element.children].some(shows)
          if (!isLeaf || !shows(element) || box.top < titleBottom) continue
          top = Math.min(top, box.top)
          bottom = Math.max(bottom, box.bottom)
        }
        let scroller: Element = title
        while (scroller.parentElement && !['auto', 'scroll'].includes(getComputedStyle(scroller).overflowY)) {
          scroller = scroller.parentElement
        }
        const contentEnd = scroller.getBoundingClientRect().top + scroller.clientTop - scroller.scrollTop + scroller.scrollHeight
        return { top: Math.round(top - titleBottom), bottom: Math.round(contentEnd - bottom) }
      }, sheet)
    },

    async pageScrollWidth() {
      return page.evaluate(() => document.documentElement.scrollWidth)
    },
  }
}

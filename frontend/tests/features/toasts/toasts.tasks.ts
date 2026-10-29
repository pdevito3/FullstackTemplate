import { expect, type Locator, type Page } from '@playwright/test'
import { takesUpSpace } from '@tests/support/screen'
import { sharedComponentsPage } from '@tests/support/shared-components'
import { toastDemo, type ToastButton } from './toasts.locators'

// The toasts pause and restart their timers in ResizeObserver callbacks. The browser runs all the callbacks of one
// rendering step in one task, so the next command of the test comes after all of them.
async function nextResizeObserverCallbacks(page: Page) {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        const observer = new ResizeObserver(() => {
          observer.disconnect()
          resolve()
        })
        observer.observe(document.body)
      }),
  )
}

export function toastDemoPage(page: Page) {
  const at = toastDemo(page)

  return {
    at,

    // The clock stands still from the start, so only letTimePass moves the timers of the toasts. Real time that a slow
    // browser takes does not count. The animations wait too, so a sheet that opens stays transparent until time passes.
    async controlTheClock() {
      const start = new Date('2026-01-05T09:00:00Z')
      await page.clock.install({ time: start })
      await page.clock.pauseAt(start.getTime() + 60_000)
    },

    async open() {
      await page.goto(sharedComponentsPage)
      await expect(at.heading).toBeVisible()
    },

    async waitForDevtoolsBadge() {
      await expect(at.devtoolsBadge).toBeVisible()
    },

    async showToastsByKeyboard(...buttons: ToastButton[]) {
      for (const button of buttons) await at.showToast(button).press('Enter')
      await expect(at.toasts).toHaveCount(buttons.length)
    },

    // The sheet plays its slide-in on the real time of the page, not on the clock of the test. A sheet that closes
    // before the slide-in ends waits for the clock, so the task waits for the end of the slide-in.
    async openBottomSheet() {
      await at.openDialog.press('Enter')
      await expect(at.dialog).toBeVisible()
      await at.sheet.evaluate((sheet) => Promise.all(sheet.getAnimations().map((animation) => animation.finished)))
      await nextResizeObserverCallbacks(page)
    },

    async closeBottomSheet() {
      await at.closeDialog.press('Enter')
      await expect(at.dialog).toBeHidden()
      await nextResizeObserverCallbacks(page)
    },

    async moveFocusToBottomSheet() {
      await at.dialogTitle.click()
      await expect(at.dialog).toBeFocused()
    },

    async letTimePass(ms: number) {
      await page.clock.runFor(ms)
      await nextResizeObserverCallbacks(page)
    },

    async moveFocusToToasts() {
      await page.keyboard.press('F6')
      await expect(at.region).toBeFocused()
    },

    async pressTab(times: number) {
      for (let i = 0; i < times; i++) await page.keyboard.press('Tab')
    },

    async shownToasts() {
      const shown: string[] = []
      for (const toast of await at.toasts.all()) {
        if (await takesUpSpace(toast)) shown.push((await toast.innerText()).split('\n')[0])
      }
      return shown
    },

    async shownToastsAsTimePasses(...stepsMs: number[]) {
      const samples = [{ afterMs: 0, toasts: await this.shownToasts() }]
      for (const ms of stepsMs) {
        await this.letTimePass(ms)
        samples.push({ afterMs: samples[samples.length - 1].afterMs + ms, toasts: await this.shownToasts() })
      }
      return samples
    },

    async overlapAreaOf(a: Locator, b: Locator) {
      if (!(await a.isVisible()) || !(await b.isVisible())) return 0
      const [boxA, boxB] = [await a.boundingBox(), await b.boundingBox()]
      if (!boxA || !boxB) return 0
      const width = Math.min(boxA.x + boxA.width, boxB.x + boxB.width) - Math.max(boxA.x, boxB.x)
      const height = Math.min(boxA.y + boxA.height, boxB.y + boxB.height) - Math.max(boxA.y, boxB.y)
      return Math.max(0, width) * Math.max(0, height)
    },

    async hasFocusRing(element: Locator) {
      return element.evaluate((node) => {
        const { outlineStyle, outlineWidth, boxShadow } = getComputedStyle(node)
        if (outlineStyle !== 'none' && parseFloat(outlineWidth) > 0) return true
        const spreads = [...boxShadow.matchAll(/-?[\d.]+px -?[\d.]+px [\d.]+px (?<spread>-?[\d.]+)px/g)].map((m) => Number(m.groups?.spread))
        return spreads.some((spread) => spread >= 1)
      })
    },
  }
}

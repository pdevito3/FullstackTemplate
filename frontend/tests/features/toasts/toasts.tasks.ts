import { expect, type Locator, type Page } from '@playwright/test'
import { takesUpSpace } from '@tests/support/screen'
import { toastDemo, type ToastButton } from './toasts.locators'

export function toastDemoPage(page: Page) {
  const at = toastDemo(page)

  return {
    at,

    async open() {
      await page.goto('/components')
      await expect(at.heading).toBeVisible()
    },

    async showToastsByKeyboard(...buttons: ToastButton[]) {
      for (const button of buttons) await at.showToast(button).press('Enter')
      await expect(at.toasts).toHaveCount(buttons.length)
    },

    async openBottomSheet() {
      await at.openDialog.press('Enter')
      await expect(at.dialog).toBeVisible()
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

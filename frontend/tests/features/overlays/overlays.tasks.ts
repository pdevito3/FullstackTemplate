import { expect, type Locator, type Page } from '@playwright/test'
import { overlayDemo, type Overlay } from './overlays.locators'

export function overlayDemoPage(page: Page) {
  const at = overlayDemo(page)

  return {
    at,

    async open() {
      await page.goto('/components')
      await expect(at.heading).toBeVisible()
      await expect(at.devtoolsBadge).toBeVisible()
    },

    async openOverlay(overlay: Overlay) {
      await at.openButton(overlay).press('Enter')
      await expect(at.dialog).toBeVisible()
    },

    async closeOverlay() {
      await page.keyboard.press('Escape')
      await expect(at.dialog).toBeHidden()
    },

    async overlapAreaOf(a: Locator, b: Locator) {
      if (!(await a.isVisible()) || !(await b.isVisible())) return 0
      const [boxA, boxB] = [await a.boundingBox(), await b.boundingBox()]
      if (!boxA || !boxB) return 0
      const width = Math.min(boxA.x + boxA.width, boxB.x + boxB.width) - Math.max(boxA.x, boxB.x)
      const height = Math.min(boxA.y + boxA.height, boxB.y + boxB.height) - Math.max(boxA.y, boxB.y)
      return Math.max(0, width) * Math.max(0, height)
    },
  }
}

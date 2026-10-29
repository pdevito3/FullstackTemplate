import { expect, type Locator, type Page } from '@playwright/test'
import { weatherPaths } from '@tests/support/api/weather-api'
import { appShell } from './app-shell.locators'

export function appShellPage(page: Page) {
  const at = appShell(page)
  const warnings: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'warning') warnings.push(message.text())
  })

  return {
    at,
    warnings,

    async openAbout() {
      await page.goto('/about')
      await expect(at.aboutText).toBeVisible()
    },

    async openMobileSidebar() {
      await at.sidebarTrigger.click()
      await expect(at.mobileSidebar).toBeVisible()
      await expect.poll(() => at.mobileSidebar.evaluate((sheet) => sheet.getAnimations().length)).toBe(0)
    },

    async openMissingPage() {
      await page.goto('/no-such-page')
      await expect(at.currentPage).toBeVisible()
    },

    async goHome() {
      const settled = page.waitForResponse(
        (r) => r.request().method() === 'GET' && new URL(r.url()).pathname === weatherPaths.list,
      )
      await at.homeLink.click()
      await settled
    },

    async pressHomeLink() {
      const settled = page.waitForResponse(
        (r) => r.request().method() === 'GET' && new URL(r.url()).pathname === weatherPaths.list,
      )
      await at.homeLink.press('Enter')
      await settled
    },

    async pressAboutNavigationLink() {
      await at.aboutNavigationLink.press('Enter')
      await expect(at.aboutText).toBeVisible()
    },

    async openAboutFromMobileSidebar() {
      await at.aboutNavigationLink.click()
      await expect(at.aboutText).toBeVisible()
      await expect(at.mobileSidebar).toHaveCount(0)
    },

    async clickBlankContent() {
      await at.aboutText.click()
    },

    async focusedElement() {
      return page.evaluate(() => (document.activeElement === document.body ? 'body' : document.activeElement?.tagName))
    },

    async addWideContent(width: number) {
      await at.mainLandmarks.evaluate((main, width) => {
        const scroller = document.createElement('div')
        scroller.className = 'overflow-x-auto'
        scroller.role = 'region'
        scroller.ariaLabel = 'Wide content'
        const content = document.createElement('div')
        content.style.width = `${width}px`
        content.textContent = 'Wide content'
        scroller.append(content)
        main.append(scroller)
      }, width)
    },

    async pageSideScrollRange() {
      return page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    },

    async sideScrollRange(locator: Locator) {
      return locator.evaluate((element) => element.scrollWidth - element.clientWidth)
    },

    async verticalCenter(locator: Locator) {
      const box = await locator.boundingBox()
      if (!box) throw new Error('The element has no box')
      return box.y + box.height / 2
    },

    async pageScrollRange() {
      return page.evaluate(() => document.documentElement.scrollHeight - document.documentElement.clientHeight)
    },
  }
}

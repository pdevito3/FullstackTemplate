import { expect, type Page } from '@playwright/test'
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

    async pageScrollRange() {
      return page.evaluate(() => document.documentElement.scrollHeight - document.documentElement.clientHeight)
    },
  }
}

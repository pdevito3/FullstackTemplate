import { expect, type Page } from '@playwright/test'
import { appShell } from './app-shell.locators'

export function appShellPage(page: Page) {
  const at = appShell(page)

  return {
    at,

    async openAbout() {
      await page.goto('/about')
      await expect(at.aboutText).toBeVisible()
    },

    async pageScrollRange() {
      return page.evaluate(() => document.documentElement.scrollHeight - document.documentElement.clientHeight)
    },
  }
}

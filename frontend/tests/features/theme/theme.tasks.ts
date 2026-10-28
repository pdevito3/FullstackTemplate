import { expect, type Page } from '@playwright/test'
import { themeToggle, type ThemeChoice } from './theme.locators'

export function themePage(page: Page) {
  const at = themeToggle(page)

  return {
    at,

    async open() {
      await page.goto('/components')
      await expect(at.heading).toBeVisible()
    },

    async useOsScheme(scheme: 'light' | 'dark') {
      await page.emulateMedia({ colorScheme: scheme })
    },

    async choose(choice: ThemeChoice) {
      await at.choice(choice).press('Enter')
    },

    /** Loads the page again without the app script, so only index.html can set the theme class. */
    async reloadWithoutTheApp() {
      await page.route('**/src/main.tsx', (route) => route.abort())
      await page.reload()
    },

    async themeClass() {
      return page.evaluate(() => document.documentElement.className)
    },

    /** The scheme that the browser uses for the scrollbars and the native controls of the page. */
    async colorScheme() {
      return page.evaluate(() => getComputedStyle(document.documentElement).colorScheme)
    },
  }
}

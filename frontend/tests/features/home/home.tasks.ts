import { expect, type Page } from '@playwright/test'
import { home } from './home.locators'

export function homePage(page: Page) {
  const at = home(page)

  return {
    at,

    async open() {
      await page.goto('/')
      await expect(at.heading).toBeVisible()
    },

    async logIn() {
      await at.login.click()
      await page.waitForURL('**/bff/login**')
    },

    async logOut() {
      await at.logout.click()
      await page.waitForURL('**/bff/logout**')
    },
  }
}

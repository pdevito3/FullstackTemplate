import { expect, type Locator, type Page } from '@playwright/test'
import { bffPaths } from '@tests/support/api/bff-api'
import { weatherPaths } from '@tests/support/api/weather-api'
import { weatherForecast } from './weather-forecast.locators'

export function weatherForecastPage(page: Page) {
  const at = weatherForecast(page)
  // The exact path: `/api/v1/weather/secure` also starts with `/api/v1/weather`.
  const answered = (path: string) =>
    page.waitForResponse((r) => r.request().method() === 'GET' && new URL(r.url()).pathname === path)

  return {
    at,

    async open() {
      const settled = Promise.all([answered(bffPaths.user), answered(weatherPaths.list)])
      await page.goto('/')
      await expect(at.heading).toBeVisible()
      await settled
    },

    async openWhileLoading() {
      await page.goto('/')
      await expect(at.loading).toBeVisible()
    },

    async runningAnimationsIn(element: Locator) {
      return element.evaluate((node) => node.getAnimations({ subtree: true }).length)
    },

    async showInCelsius() {
      await at.celsius.click()
    },

    async refresh() {
      const settled = answered(weatherPaths.list)
      await at.refresh.click()
      await settled
    },

    async tryAgain() {
      const settled = answered(weatherPaths.list)
      await at.tryAgain.click()
      await settled
    },

    async showSecureForecast() {
      const settled = answered(weatherPaths.secureList)
      await at.secureToggle.click()
      await settled
    },

    // Login and logout leave the SPA: the browser navigates to the BFF.
    async logIn() {
      await at.login.click()
      await page.waitForURL((url) => url.pathname === bffPaths.login)
    },

    async logOut() {
      await at.logout.click()
      await page.waitForURL((url) => url.pathname === bffPaths.logout)
    },
  }
}

import { expect, type Locator, type Page } from '@playwright/test'
import { openAndWaitForLogin } from '@tests/support/answers'
import { bffPaths } from '@tests/support/api/bff-api'
import { session } from './session.locators'

declare global {
  /** The functions that `page.exposeFunction` adds to the page of a task below. */
  interface Window {
    reportRender: () => void
    reportToast: () => void
  }
}

export const blankPage = 'data:text/html,blank'

export function sessionPage(page: Page) {
  const at = session(page)

  const answered = (path: string) =>
    page.waitForResponse((r) => r.request().method() === 'GET' && new URL(r.url()).pathname === path)

  return {
    at,

    /** The page is gone when the login opens, so a script in the page reports each render at once. */
    async openAndGoToLogin(path: string) {
      let rendered = false
      await page.exposeFunction('reportRender', () => {
        rendered = true
      })
      await page.addInitScript(() => {
        const report = window.reportRender
        new MutationObserver(() => {
          if (document.getElementById('root')?.hasChildNodes()) report()
        }).observe(document, { childList: true, subtree: true })
      })

      await openAndWaitForLogin(page, path)

      return { rendered }
    },

    async openBlankPage() {
      await page.goto(blankPage)
    },

    async openDeepLinkAndWaitForLogin(path: string) {
      await openAndWaitForLogin(page, path)
      await page.waitForLoadState()
    },

    async openFailurePage(path: string, heading: Locator) {
      await page.goto(path)
      await expect(heading).toBeVisible()
    },

    /** The page is gone when the BFF opens, so the wait is for the request of the BFF. */
    async pressAndLeaveFor(button: Locator, path: string) {
      const left = answered(path)
      await expect(button).toBeEnabled()
      await button.click()
      await left
      await expect.poll(() => new URL(page.url()).pathname).toBe(path)
    },

    async open(path: string) {
      await page.goto(path)
      await expect(at.navigation).toBeVisible()
    },

    /** The page is gone when the login opens, so a script in the page reports each toast at once. */
    async followAndGoToLogin(link: Locator) {
      let toasted = false
      await page.exposeFunction('reportToast', () => {
        toasted = true
      })
      await page.evaluate(() => {
        const report = window.reportToast
        new MutationObserver(() => {
          if (document.querySelector('[role="alertdialog"]')) report()
        }).observe(document.body, { childList: true, subtree: true })
      })

      const login = answered(bffPaths.login)
      await link.click()
      await login
      await expect.poll(() => new URL(page.url()).pathname).toBe(bffPaths.login)

      return { toasted }
    },

    /** A 401 of the API makes the app check the session again. */
    async followAndWaitForTheSessionCheck(link: Locator) {
      const checked = answered(bffPaths.user)
      await link.click()
      await checked
    },

    async openWhileTheCheckFails(path: string) {
      await page.goto(path)
      await expect(at.checkFailed).toBeVisible()
    },

    async pressTryAgain() {
      await at.tryAgain.focus()
      await page.keyboard.press('Enter')
    },

    async tryAgain() {
      const settled = answered(bffPaths.user)
      await at.tryAgain.click()
      await settled
    },
  }
}

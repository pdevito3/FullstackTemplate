import { expect, type Locator, type Page } from '@playwright/test'
import { bffPaths } from '@tests/support/api/bff-api'
import { session } from './session.locators'

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
        const report = (window as unknown as { reportRender: () => void }).reportRender
        new MutationObserver(() => {
          if (document.getElementById('root')?.hasChildNodes()) report()
        }).observe(document, { childList: true, subtree: true })
      })

      const login = answered(bffPaths.login)
      // The SPA leaves the page as it loads, so the wait is for the commit and not for the load.
      await page.goto(path, { waitUntil: 'commit' })
      await login
      return { rendered }
    },

    async openBlankPage() {
      await page.goto(blankPage)
    },

    async openDeepLinkAndWaitForLogin(path: string) {
      await page.goto(path, { waitUntil: 'commit' })
      await expect.poll(() => new URL(page.url()).pathname).toBe(bffPaths.login)
      await page.waitForLoadState()
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
        const report = (window as unknown as { reportToast: () => void }).reportToast
        new MutationObserver(() => {
          if (document.querySelector('[role="alertdialog"]')) report()
        }).observe(document.body, { childList: true, subtree: true })
      })

      const login = answered(bffPaths.login)
      await link.click()
      await login
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

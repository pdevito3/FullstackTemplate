import { expect, type Page } from '@playwright/test'
import { bffPaths } from '@tests/support/api/bff-api'
import { membershipPaths } from '@tests/support/api/membership-api'
import { actorSubject } from '@tests/support/auth/actors'
import { startTenant } from './start-tenant.locators'

export function startTenantPage(page: Page) {
  const at = startTenant(page)
  const answered = (method: string, path: string) =>
    page.waitForResponse((r) => r.request().method() === method && new URL(r.url()).pathname === path)

  return {
    at,

    /** Opens a page as a signed-in actor, and waits for the lookup of its user record. */
    async openAsSignedIn(path: '/' | '/start-tenant') {
      const settled = answered('GET', membershipPaths.userOf(actorSubject))
      await page.goto(path)
      await settled
    },

    async openAnonymous() {
      const settled = answered('GET', bffPaths.user)
      await page.goto('/start-tenant')
      await expect(at.heading).toBeVisible()
      await settled
    },

    async followNotice() {
      await at.noticeLink.click()
      await expect(at.form).toBeVisible()
    },

    async goHome() {
      await at.homeLink.click()
      await expect(page).toHaveURL('/')
    },

    async submitEmpty() {
      await at.submit.click()
      await expect(at.formErrors).toHaveCount(1)
    },

    async start(tenantName: string) {
      await at.field('Tenant name').fill(tenantName)
      const settled = answered('POST', membershipPaths.start)
      await at.submit.click()
      await settled
    },
  }
}

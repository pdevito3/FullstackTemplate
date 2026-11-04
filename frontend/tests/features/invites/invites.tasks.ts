import { expect, type Page } from '@playwright/test'
import { invitesPaths } from '@tests/support/api/invites-api'
import { tenantsPaths } from '@tests/support/api/tenants-api'
import { invites, join } from './invites.locators'

const answeredBy = (page: Page) => (method: string, path: string) =>
  page.waitForResponse((r) => r.request().method() === method && new URL(r.url()).pathname === path)

async function pageOverflow(page: Page) {
  return page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
}

export function usersWithInvites(page: Page) {
  const at = invites(page)
  const answered = answeredBy(page)

  return {
    at,

    pageOverflow: () => pageOverflow(page),

    linkFieldOverflow: () => at.linkField.evaluate((field) => field.scrollWidth - field.clientWidth),

    /** Opens /users and waits for the tenant, so that the button and the list have what they need. */
    async open(tenantId = 'tnt_1', path = '/users') {
      const settled = answered('GET', tenantsPaths.one(tenantId))
      await page.goto(path)
      await settled
      await expect(at.usersHeading).toBeVisible()
    },

    async openWithList(path = '/users') {
      const settled = answered('GET', invitesPaths.list)
      await page.goto(path)
      await settled
      await expect(at.invitesHeading).toBeVisible()
    },

    async goToNextPage() {
      const settled = answered('GET', invitesPaths.list)
      await at.nextPage.click()
      await settled
    },

    async openDialog() {
      await at.inviteButton.click()
      await expect(at.inviteDialog).toBeVisible()
    },

    async cancelDialog() {
      await at.cancelInvite.click()
      await expect(at.inviteDialog).toBeHidden()
    },

    async pickRole(role: string) {
      await at.roleSelect.click()
      await at.roleOption(role).click()
      await expect(at.roleSelect).toHaveAccessibleName(`${role} Role`)
    },

    async submitDialog() {
      const settled = answered('POST', invitesPaths.list)
      await at.createLink.click()
      await settled
    },

    /** Creates the invite and waits for the list to read it, so that a test can read the requests. */
    async createInvite() {
      const settled = Promise.all([answered('POST', invitesPaths.list), answered('GET', invitesPaths.list)])
      await at.createLink.click()
      await settled
      await expect(at.linkField).toBeVisible()
    },

    async copyTheLink() {
      await at.copyLink.click()
      await expect(at.linkCopied).toBeVisible()
      return page.evaluate(() => navigator.clipboard.readText())
    },

    async closeLinkDialog() {
      await at.done.click()
      await expect(at.linkDialog).toBeHidden()
    },

    async askToRevoke(email: string) {
      await at.revokeOf(email).click()
      await expect(at.revokeDialog).toBeVisible()
    },

    async keepInvite() {
      await at.keepInvite.click()
      await expect(at.revokeDialog).toBeHidden()
    },

    async confirmRevoke(id: string) {
      const settled = Promise.all([answered('POST', invitesPaths.revoke(id)), answered('GET', invitesPaths.list)])
      await at.confirmRevoke.click()
      await settled
    },

    /** Presses Escape in the dialog of the link, and gives a close the time to start. */
    async pressEscape() {
      await page.keyboard.press('Escape')
      await expect(at.linkField).toBeVisible()
    },
  }
}

/** Two frames, so that the page has rendered each answer that it got. */
const nextFrames = (page: Page) =>
  page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))))

export function joinPage(page: Page) {
  const at = join(page)
  const context = page.context()
  const answered = answeredBy(page)
  const requestedUrls: string[] = []
  page.on('request', (request) => requestedUrls.push(request.url()))

  return {
    at,

    pageOverflow: () => pageOverflow(page),

    urlsWith: (text: string) => requestedUrls.filter((url) => decodeURIComponent(url).includes(text)),

    /** The value that the sign-in and a reload read. It stays in this tab. */
    storedToken: () => page.evaluate(() => sessionStorage.getItem('invite-token')),

    historyLength: () => page.evaluate(() => window.history.length),

    /** Opens the link as a signed-in user, and waits for the preview. */
    async openLink(token: string, path = '/join') {
      const settled = answered('POST', invitesPaths.preview)
      await page.goto(`${path}#token=${token}`)
      await settled
    },

    /** Opens the link as a member or with a link that is not good, so that no preview goes out. */
    async openLinkWithNoPreview(path: string) {
      await page.goto(path)
      await expect(at.heading).toBeVisible()
    },

    async openLinkAndGoToLogin(token: string, path = '/join') {
      // The SPA leaves the page as it loads, so the wait is for the commit and not for the load. React StrictMode starts
      // the login twice in development, so wait for the address and not for the first answer, which an abort can follow.
      await page.goto(`${path}#token=${token}`, { waitUntil: 'commit' })
      await expect(page).toHaveURL(/\/bff\/login/)
    },

    /** The IdP sends the browser back to the return URL, in the same tab. */
    async comeBackFromLogin(returnUrl: string) {
      const settled = answered('POST', invitesPaths.preview)
      await page.goto(returnUrl)
      await settled
    },

    async reload() {
      const settled = answered('POST', invitesPaths.preview)
      await page.reload()
      await settled
    },

    /** The network comes back, and TanStack Query reads the preview again. A failure retries one time. */
    async readPreviewAgainAfterAReconnect(requests: number) {
      const settled = (async () => {
        for (let i = 0; i < requests; i++) await answered('POST', invitesPaths.preview)
      })()
      await context.setOffline(true)
      await context.setOffline(false)
      await settled
      await nextFrames(page)
    },

    /** A second link in the same tab is a change of the fragment, as when the user pastes it in the address bar. */
    async pasteLink(token: string) {
      const settled = answered('POST', invitesPaths.preview)
      await page.evaluate((hash) => { window.location.hash = hash }, `token=${token}`)
      await settled
    },

    async goBack() {
      await page.goBack()
    },

    async open(path: string) {
      await page.goto(path)
    },

    async fill({ firstName, lastName, email, username }: { firstName: string; lastName: string; email: string; username: string }) {
      await at.firstName.fill(firstName)
      await at.lastName.fill(lastName)
      await at.email.fill(email)
      await at.username.fill(username)
    },

    async claim() {
      const settled = answered('POST', invitesPaths.claim)
      await at.submit.click()
      await settled
    },

    /** Claims, and waits for the check of a double submit: the user record, then its tenant. */
    async claimAndCheckMembership(tenantId?: string) {
      const claimed = answered('POST', invitesPaths.claim)
      const checked = page.waitForResponse((r) => new URL(r.url()).pathname.startsWith('/api/v1/users/by-identifier/'))
      const tenant = tenantId ? answered('GET', tenantsPaths.one(tenantId)) : Promise.resolve()
      await at.submit.click()
      await Promise.all([claimed, checked, tenant])
    },

    async claimAndGoHome() {
      const settled = answered('POST', invitesPaths.claim)
      await at.submit.click()
      await settled
      await expect(page).toHaveURL('/')
    },
  }
}

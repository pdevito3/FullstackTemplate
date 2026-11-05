import { membershipApi, membershipPaths } from '@tests/support/api/membership-api'
import { usersApi } from '@tests/support/api/users-api'
import { actorSubject, as } from '@tests/support/auth/actors'
import { aStartedTenant, aUser } from '@tests/support/builders/user-builder'
import { expect, test } from '@tests/support/fixtures'
import { usersPage } from '../users/users.tasks'
import { startTenantPage } from './start-tenant.tasks'

test.describe('a signed-in user with no tenant', () => {
  test.use({ actor: as.userWithNoTenant('Ada Lovelace') })

  test('follows the notice on the home page and starts a tenant as its admin', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.startReturns(aStartedTenant('Acme')))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await start.followNotice()

    await start.start('Acme')

    expect(api.last('post', membershipPaths.start)?.body).toEqual({
      tenantName: 'Acme',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.com',
      username: 'ada',
      identifier: '1',
      role: 'Admin',
    })
    await expect(start.at.started('Acme')).toBeVisible()
    await expect(start.at.homeLink).toBeFocused()

    await start.goHome()

    await expect(start.at.homeHeading).toBeFocused()
  })

  test('sees no notice on the home page after the start', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.startReturns(aStartedTenant('Acme')))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')
    // The Server has the user record after the start.
    api.mock(...membershipApi.userIs(as.user()))
    await start.start('Acme')

    await start.goHome()

    await expect(start.at.notice).toHaveCount(0)
  })

  test('sees the refusal of the Server next to the tenant name', async ({ page, api }) => {
    api.mock(membershipApi.startRefuses('tenantName', 'A tenant with this name exists. Choose another name.'))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')

    await start.start('Acme')

    await expect(start.at.formErrors).toHaveText('A tenant with this name exists. Choose another name.')
    await expect(start.at.field('Tenant name')).toHaveAttribute('aria-invalid', 'true')
    await expect(start.at.field('Tenant name')).toBeFocused()
    expect(await start.at.toasts.count()).toBe(0)
  })

  test('who got a user record in another tab sees that they are a member after the refusal', async ({ page, api }) => {
    api.mock(membershipApi.startRefuses('identifier', 'A user with this identifier already exists.'))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')
    api.mock(...membershipApi.userIs(as.user()))

    await start.start('Acme')

    await expect(start.at.alreadyMember).toBeVisible()
    await expect(start.at.form).toHaveCount(0)
  })

  test('must give a tenant name', async ({ page, api }) => {
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')

    await start.submitEmpty()

    await expect(start.at.formErrors).toHaveText('Enter a tenant name.')
    expect(api.all('post', membershipPaths.start)).toHaveLength(0)
  })
})

for (const viewport of [
  { width: 1280, height: 800 },
  { width: 320, height: 640 },
]) {
  test.describe(`the notice at ${viewport.width}x${viewport.height}`, () => {
    test.use({ actor: as.userWithNoTenant('Ada Lovelace'), viewport })

    test('spans the main area under the header, so that it does not depend on the width of a page', async ({ page, api }) => {
      api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }))
      const start = startTenantPage(page)

      await start.openAsSignedIn('/')

      await expect(start.at.notice).toBeVisible()
      const notice = await start.at.notice.boundingBox()
      const main = await start.at.main.boundingBox()
      expect(notice!.x).toBeCloseTo(main!.x, 0)
      expect(notice!.width).toBeCloseTo(main!.width, 0)
    })
  })
}

test.describe('a signed-in user whose lookup the Server refuses', () => {
  test.use({ actor: as.userWithNoTenant('Ada Lovelace') })

  const refusal = 'You are not a member of a tenant. Ask a tenant admin to add you.'

  test('sees the reason of the Server with no automatic retry, and Try Again asks again', async ({ page, api }) => {
    api.mock(membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')

    await expect(start.at.lookupFailure).toContainText(refusal)
    expect(api.all('get', membershipPaths.userOf(actorSubject))).toHaveLength(1)
    expect(await start.at.toasts.count()).toBe(0)

    api.mock(...membershipApi.userIs(as.userWithNoTenant('Ada Lovelace')))
    await start.at.tryAgain.click()

    await expect(start.at.form).toBeVisible()
    await expect(start.at.lookupFailure).toHaveCount(0)
  })

  test('sees the notice after Try Again on the home page', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.checkFailure).toContainText(refusal)
    await expect(start.at.notice).toHaveCount(0)

    api.mock(...membershipApi.userIs(as.userWithNoTenant('Ada Lovelace')))
    await start.at.tryAgain.click()

    await expect(start.at.notice).toBeVisible()
    await expect(start.at.checkFailure).toHaveCount(0)
  })

  test('sees the reason again after Try Again when the Server still refuses', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.checkFailure).toContainText(refusal)

    await start.at.tryAgain.click()

    await expect.poll(() => api.all('get', membershipPaths.userOf(actorSubject)).length).toBe(2)
    await expect(start.at.checkFailure).toContainText(refusal)
    await expect(start.at.tryAgain).toBeVisible()
  })

  test('keeps the reason and the focus on Try Again while the check runs and after a new refusal', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.checkFailure).toContainText(refusal)

    const answer = api.hold(membershipApi.lookupRefuses(refusal))
    await start.at.tryAgain.focus()
    await page.keyboard.press('Enter')

    await expect.poll(() => api.all('get', membershipPaths.userOf(actorSubject)).length).toBe(2)
    await expect(start.at.checkFailure).toContainText(refusal)
    await expect(start.at.tryAgain).toBeDisabled()
    await expect(start.at.tryAgain).toBeFocused()

    answer()

    await expect(start.at.tryAgain).toBeEnabled()
    await expect(start.at.checkFailure).toContainText(refusal)
    await expect(start.at.tryAgain).toBeFocused()
  })

  test('moves the focus to the link of the notice after Try Again', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.checkFailure).toContainText(refusal)

    api.mock(...membershipApi.userIs(as.userWithNoTenant('Ada Lovelace')))
    await start.at.tryAgain.click()

    await expect(start.at.noticeLink).toBeFocused()
  })

  test('moves the focus to the page heading after Try Again finds a member', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.checkFailure).toContainText(refusal)

    api.mock(...membershipApi.userIs(as.user('Ada Lovelace')))
    await start.at.tryAgain.click()

    await expect(start.at.checkFailure).toHaveCount(0)
    await expect(start.at.homeHeading).toBeFocused()
  })

  test('keeps the focus where the user moved it while the answer of Try Again was on the way', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.checkFailure).toContainText(refusal)

    const answer = api.hold(...membershipApi.userIs(as.user('Ada Lovelace')))
    await start.at.tryAgain.click()
    await expect.poll(() => api.all('get', membershipPaths.userOf(actorSubject)).length).toBe(2)
    await start.at.aboutLink.focus()
    answer()

    await expect(start.at.checkFailure).toHaveCount(0)
    await expect(start.at.aboutLink).toBeFocused()
  })

  test('keeps the focus on a link that the user clicks after Try Again got a new refusal', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.checkFailure).toContainText(refusal)
    await start.at.tryAgain.click()
    await expect.poll(() => api.all('get', membershipPaths.userOf(actorSubject)).length).toBe(2)
    await expect(start.at.tryAgain).toBeEnabled()

    api.mock(...membershipApi.userIs(as.userWithNoTenant('Ada Lovelace')))
    await start.goToAbout()

    await expect(start.at.notice).toBeVisible()
    await expect(start.at.aboutLink).toBeFocused()
  })

  test('sees the notice after a click on another page, with no reload', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.checkFailure).toContainText(refusal)

    api.mock(...membershipApi.userIs(as.userWithNoTenant('Ada Lovelace')))
    await start.goToAbout()

    await expect(start.at.notice).toBeVisible()
    await expect(start.at.checkFailure).toHaveCount(0)
    await expect(start.at.aboutLink).toBeFocused()
  })

  test.describe('in a short window', () => {
    test.use({ viewport: { width: 320, height: 240 } })

    test('does not scroll the page when the check stops failing with no press', async ({ page, api }) => {
      api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
      const start = startTenantPage(page)
      await start.openAsSignedIn('/')
      await expect(start.at.checkFailure).toContainText(refusal)
      api.mock(...membershipApi.userIs(as.userWithNoTenant('Ada Lovelace')))
      await start.scrollPastTheBanner()

      await start.reconnect()

      await expect(start.at.noticeLink).toBeFocused()
      const link = await start.at.noticeLink.boundingBox()
      expect(link!.y + link!.height).toBeLessThan(0)
    })
  })

  test('sends no lookup for a change of the search of a page, and one lookup for another page', async ({ page, api }) => {
    api.mock(
      usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }),
      membershipApi.lookupRefuses(refusal),
      usersApi.listReturns([aUser().identifiedBy('other')], { total: 40 }),
    )
    const start = startTenantPage(page)
    const users = usersPage(page)
    await users.openList()
    await expect(start.at.checkFailure).toContainText(refusal)
    const lookups = () => api.all('get', membershipPaths.userOf(actorSubject)).length
    const before = lookups()

    await users.goToNextPage()

    expect(lookups()).toBe(before)

    await start.goToAboutAndAskAgain()

    expect(lookups()).toBe(before + 1)
  })

  test('shows the loading state, not the general message, on a page that mounts while the check runs', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')
    await expect(start.at.lookupFailure).toContainText(refusal)
    await start.goToAboutAndAskAgain()
    await expect(start.at.tryAgain).toBeEnabled()

    const answer = api.hold(membershipApi.lookupRefuses(refusal))
    await start.goBackTo('/start-tenant')

    await expect(start.at.loadingAccount).toBeVisible()
    await expect(start.at.lookupFailure).toHaveCount(0)

    answer()

    await expect(start.at.lookupFailure).toContainText(refusal)
  })

  test('shows the failure one time while the next page loads', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')
    await expect(start.at.lookupFailure).toContainText(refusal)
    const loadAbout = await api.holdPageSource(/\/routes\/about\.tsx/)

    await start.goToAbout()

    await expect(start.at.checkFailure).toHaveCount(0)
    await expect(start.at.lookupFailure).toContainText(refusal)

    loadAbout()

    await expect(start.at.aboutHeading).toBeVisible()
    await expect(start.at.checkFailure).toContainText(refusal)
    await expect(start.at.lookupFailure).toHaveCount(0)
  })

  test('moves the focus to the heading of the start page after its Try Again finds a member', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')
    await expect(start.at.lookupFailure).toContainText(refusal)

    const answer = api.hold(...membershipApi.userIs(as.user('Ada Lovelace')))
    await start.at.tryAgain.click()

    await expect(start.at.tryAgain).toBeDisabled()
    await expect(start.at.lookupFailure).toContainText(refusal)
    await expect(start.at.tryAgain).toBeFocused()

    answer()

    await expect(start.at.alreadyMember).toBeVisible()
    await expect(start.at.heading).toBeFocused()
  })

  test('keeps the focus of the form after Try Again of the start page finds no record', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), membershipApi.lookupRefuses(refusal))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')
    await expect(start.at.lookupFailure).toContainText(refusal)

    api.mock(...membershipApi.userIs(as.userWithNoTenant('Ada Lovelace')))
    await start.at.tryAgain.click()

    await expect(start.at.field('Tenant name')).toBeFocused()
  })
})

test.describe('a member of a tenant', () => {
  test.use({ actor: as.user('Bob Smith') })

  test('is told on the start page that they have a tenant, and sees no notice on the home page', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Bob Smith' }))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')
    await expect(start.at.alreadyMember).toBeVisible()
    await expect(start.at.form).toHaveCount(0)

    // The layout stays mounted, so the notice reads the membership that the page showed.
    await start.goHome()

    await expect(start.at.notice).toHaveCount(0)
    await expect(start.at.checkFailure).toHaveCount(0)
  })

  test('sends no new lookup for a click on another page', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: actorSubject, name: 'Bob Smith' }))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')
    await expect(start.at.homeHeading).toBeVisible()

    // Two clicks, because a request of the first click reaches the mock before the second page shows.
    await start.goToAbout()
    await start.goHomeInSidebar()

    await expect(start.at.homeHeading).toBeVisible()
    expect(api.all('get', membershipPaths.userOf(actorSubject))).toHaveLength(1)
  })
})

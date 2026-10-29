import { membershipApi, membershipPaths } from '@tests/support/api/membership-api'
import { usersApi } from '@tests/support/api/users-api'
import { actorSubject, as } from '@tests/support/auth/actors'
import { aStartedTenant } from '@tests/support/builders/user-builder'
import { expect, test } from '@tests/support/fixtures'
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

    await expect.poll(() => page.evaluate(() => document.activeElement?.tagName)).not.toBe('BODY')
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

test.describe('a signed-in user whose lookup the Server refuses', () => {
  test.use({ actor: as.userWithNoTenant('Ada Lovelace') })

  test('sees the reason of the Server, and no Try Again that cannot help', async ({ page, api }) => {
    api.mock(membershipApi.lookupRefuses('You are not a member of a tenant. Ask a tenant admin to add you.'))
    const start = startTenantPage(page)
    await start.openAsSignedIn('/start-tenant')

    await expect(start.at.lookupFailure).toHaveText('You are not a member of a tenant. Ask a tenant admin to add you.')
    await expect(start.at.tryAgain).toHaveCount(0)
    expect(api.all('get', membershipPaths.userOf(actorSubject))).toHaveLength(1)
    expect(await start.at.toasts.count()).toBe(0)
  })

  test('sees the reason of the Server on the home page too', async ({ page, api }) => {
    api.mock(
      usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }),
      membershipApi.lookupRefuses('You are not a member of a tenant. Ask a tenant admin to add you.'),
    )
    const start = startTenantPage(page)
    await start.openAsSignedIn('/')

    await expect(start.at.checkFailure).toHaveText('You are not a member of a tenant. Ask a tenant admin to add you.')
    await expect(start.at.notice).toHaveCount(0)
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
})

test.describe('an anonymous visitor', () => {
  test('is asked to log in, and the page does not look up a user record', async ({ page, api }) => {
    const start = startTenantPage(page)
    await start.openAnonymous()

    await expect(start.at.logInPrompt).toBeVisible()
    await expect(start.at.login).toBeVisible()
    expect(api.all('get', membershipPaths.userOf('1'))).toHaveLength(0)
  })
})

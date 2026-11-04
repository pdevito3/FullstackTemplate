import { bffApi, bffPaths } from '@tests/support/api/bff-api'
import { invitesApi, invitesPaths } from '@tests/support/api/invites-api'
import { membershipApi, membershipPaths } from '@tests/support/api/membership-api'
import { tenantsApi } from '@tests/support/api/tenants-api'
import { usersApi } from '@tests/support/api/users-api'
import { actorSubject, as } from '@tests/support/auth/actors'
import { aPreview } from '@tests/support/builders/invite-builder'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { aTenant } from '@tests/support/builders/tenant-builder'
import { aUser } from '@tests/support/builders/user-builder'
import { browserZone } from '@tests/support/clock'
import { expect, test } from '@tests/support/fixtures'
import { homePage } from '../home/home.tasks'
import { joinPage } from './invites.tasks'

const token = 'A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8S9t0U1v'
const secondToken = 'Z1y2X3w4V5u6T7s8R9q0P1o2N3m4L5k6J7i8H9g0F1e'
const notValid = 'This invite link is not valid. Ask a tenant admin for a new invite.'
const linkNeeded = 'This page needs an invite link. Open the invite link again.'
const expired = 'This invite expired. Ask a tenant admin for a new invite.'
const usedOrRevoked = 'This invite was used or revoked. Ask a tenant admin for a new invite.'
const alreadyMember = 'Your account already belongs to a tenant.'

/** `inviteExpiry` of the builder in each zone of the suite. */
const expiryIn: Record<string, string> = {
  UTC: 'Jan 15, 2099, 12:00 PM',
  'Pacific/Kiritimati': 'Jan 16, 2099, 2:00 AM',
  'Pacific/Pago_Pago': 'Jan 15, 2099, 1:00 AM',
}

test.describe('a visitor with no session opens an invite link', () => {
  test.use({ actor: as.anonymous() })

  test('goes to the login with no token in the return URL, and the token survives the sign-in', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(bffApi.loginOpens())
    const join = joinPage(page)

    await join.openLinkAndGoToLogin(token)

    expect(api.last('get', bffPaths.login)?.query.get('returnUrl')).toBe('/join')
    expect(join.urlsWith(token)).toEqual([])

    const identity = as.userWithNoTenant()
    api.mock(bffApi.userIs(identity), ...membershipApi.userIs(identity), invitesApi.previewIs(aPreview()))
    await join.comeBackFromLogin('/join')

    await expect(join.at.detail('Acme')).toBeVisible()
    expect(api.last('post', invitesPaths.preview)?.body).toEqual({ token })
    expect(join.urlsWith(token)).toEqual([])
  })

  for (const path of ['/JOIN', '/Join/', '/join//', '/about', '/no/such/page']) {
    test(`sends no token to the BFF from the changed link ${path}, and comes back to the join page`, { tag: '@INV-3' }, async ({ page, api }) => {
      api.mock(bffApi.loginOpens())
      const join = joinPage(page)

      await join.openLinkAndGoToLogin(token, path)

      expect(api.last('get', bffPaths.login)?.query.get('returnUrl')).toBe('/join')
      expect(join.urlsWith(token)).toEqual([])
      expect(await join.storedToken()).toBe(token)
    })
  }
})

test.describe('a signed-in user with no tenant opens an invite link', () => {
  test.use({ actor: as.userWithNoTenant('Ada Lovelace') })

  test('sees the tenant, the role, and the expiry in the zone of the browser', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview({ tenantName: 'Acme', role: 'Admin' })))
    const join = joinPage(page)

    await join.openLink(token)

    await expect(join.at.detail('Acme')).toBeVisible()
    await expect(join.at.detail('Admin')).toBeVisible()
    await expect(join.at.detail(expiryIn[browserZone()])).toBeVisible()
    await expect(join.at.form).toBeVisible()
  })

  test('sees no notice to start a tenant', async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)

    await join.openLink(token)

    await expect(join.at.form).toBeVisible()
    await expect(join.at.startTenantNotice).toHaveCount(0)
    await expect(page).toHaveURL('/join')
  })

  test('takes a link with a slash after the path', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)

    await join.openLink(token, '/join/')

    await expect(join.at.form).toBeVisible()
    await expect(join.at.startTenantNotice).toHaveCount(0)
    expect(api.last('post', invitesPaths.preview)?.body).toEqual({ token })
  })

  for (const path of ['/JOIN', '/join//', '/about', '/no/such/page']) {
    test(`shows the join page for the changed link ${path}`, { tag: '@INV-3' }, async ({ page, api }) => {
      api.mock(invitesApi.previewIs(aPreview()))
      const join = joinPage(page)

      await join.openLink(token, path)

      await expect(join.at.form).toBeVisible()
      await expect(page).toHaveURL('/join')
      await expect(join.at.startTenantNotice).toHaveCount(0)
      expect(api.last('post', invitesPaths.preview)?.body).toEqual({ token })
    })
  }

  test('takes the token out of the address bar with no new history entry', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)
    await join.open('/about')
    const before = await join.historyLength()

    await join.openLink(token)

    await expect(page).toHaveURL('/join')
    expect(await join.historyLength()).toBe(before + 1)

    await join.goBack()

    await expect(page).toHaveURL('/about')
  })

  test('keeps the invite after a reload of the tab', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()

    await join.reload()

    await expect(join.at.form).toBeVisible()
    await expect(page).toHaveURL('/join')
    expect(api.last('post', invitesPaths.preview)?.body).toEqual({ token })
  })

  test('sends the token only in the bodies of the preview and the claim', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()), invitesApi.claims(aUser()))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()
    api.mock(membershipApi.userIsNow(aUser().identifiedBy(actorSubject)))

    await join.claimAndGoHome()

    expect(api.last('post', invitesPaths.preview)?.body).toEqual({ token })
    expect(api.last('post', invitesPaths.claim)?.body).toMatchObject({ token })
    expect(join.urlsWith(token)).toEqual([])
    await expect(join.at.toasts).not.toContainText(token)
  })

  test('shows the invite of a second link in the same tab', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview({ tenantName: 'Acme' })))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.detail('Acme')).toBeVisible()

    api.mock(invitesApi.previewIs(aPreview({ tenantName: 'Globex' })))
    await join.pasteLink(secondToken)

    await expect(join.at.detail('Globex')).toBeVisible()
    await expect(page).toHaveURL('/join')
    expect(api.last('post', invitesPaths.preview)?.body).toEqual({ token: secondToken })
  })

  test('forgets the token at a logout, so that the next person in the tab does not get the invite', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()), usersApi.currentIs({ identifier: actorSubject, name: 'Ada Lovelace' }), bffApi.logoutOpens())
    const join = joinPage(page)
    await join.openLink(token)
    expect(await join.storedToken()).toBe(token)
    const home = homePage(page)
    await home.open()

    await home.logOut()

    expect(await join.storedToken()).toBeNull()
  })

  test('gives no invite to the next person in the tab after a session that ended with no logout here', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()
    const previews = api.all('post', invitesPaths.preview).length

    const bob = as.userWithNoTenant('Bob Smith', 'bob')
    api.mock(bffApi.userIs(bob), ...membershipApi.userIs(bob))
    await join.open('/join')

    await expect(join.at.alert(linkNeeded)).toBeVisible()
    await expect(join.at.form).toHaveCount(0)
    await expect(join.at.details).toHaveCount(0)
    expect(api.all('post', invitesPaths.preview)).toHaveLength(previews)
    expect(await join.storedToken()).toBeNull()
  })

  test('keeps the invite for the first person who comes back to the tab', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()

    await join.comeBackFromLogin('/join')

    await expect(join.at.form).toBeVisible()
    expect(api.last('post', invitesPaths.preview)?.body).toEqual({ token })
  })

  test('gives the invite to the second person who opens the link in the tab', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()

    const bob = as.userWithNoTenant('Bob Smith', 'bob')
    api.mock(bffApi.userIs(bob), ...membershipApi.userIs(bob))
    await page.goto('/about')
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()
    await join.reload()

    await expect(join.at.form).toBeVisible()
    expect(await join.storedToken()).toBe(token)
  })

  for (const [status, problem, message] of [
    ['404', aProblem().notFound(notValid), notValid],
    ['410', aProblem().gone(expired), expired],
  ] as const) {
    test(`forgets the token after a ${status} of the preview, and then shows what a tab with no token shows`, { tag: '@INV-3' }, async ({ page, api }) => {
      api.mock(invitesApi.previewFails(problem))
      const join = joinPage(page)

      await join.openLink(token)

      await expect(join.at.alert(message)).toBeVisible()
      expect(await join.storedToken()).toBeNull()

      await join.reloadWithNoPreview()

      await expect(join.at.alert(linkNeeded)).toBeVisible()
      await expect(join.at.form).toHaveCount(0)
      expect(api.all('post', invitesPaths.preview)).toHaveLength(1)
    })
  }

  test('sends no preview with no token on a reconnect after a dead link', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewFails(aProblem().gone(expired)))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.alert(expired)).toBeVisible()

    await page.context().setOffline(true)
    await page.context().setOffline(false)

    await expect(join.at.alert(expired)).toBeVisible()
    expect(api.all('post', invitesPaths.preview)).toHaveLength(1)
  })

  test('keeps the form and the typed values when a background read of the preview fails', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)
    await join.openLink(token)
    await join.at.username.fill('augusta')

    api.mock(invitesApi.previewFails(aProblem().saying('The server had a problem.')))
    await join.readPreviewAgainAfterAReconnect(2)

    await expect(join.at.form).toBeVisible()
    await expect(join.at.username).toHaveValue('augusta')
    await expect(join.at.alert('The invite did not load.')).toHaveCount(0)
  })

  test('replaces the form with the refusal when a background read of the preview finds a dead link', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()

    api.mock(invitesApi.previewFails(aProblem().gone(usedOrRevoked)))
    await join.readPreviewAgainAfterAReconnect(1)

    await expect(join.at.alert(usedOrRevoked)).toBeVisible()
    await expect(join.at.form).toHaveCount(0)
    expect(await join.storedToken()).toBeNull()
  })

  test('shows the message of the Server and no form when the link is not valid', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewFails(aProblem().notFound(notValid)))
    const join = joinPage(page)

    await join.openLink(token)

    await expect(join.at.alert(notValid)).toBeVisible()
    await expect(join.at.form).toHaveCount(0)
    await expect(join.at.details).toHaveCount(0)
    await expect(join.at.homeLink).toBeVisible()
    await expect(join.at.toasts).toHaveCount(0)
    expect(api.all('post', invitesPaths.preview)).toHaveLength(1)
  })

  for (const [name, message] of [['expired', expired], ['used or revoked', usedOrRevoked]]) {
    test(`shows the message of the Server and no form when the invite is ${name}`, { tag: '@INV-3' }, async ({ page, api }) => {
      api.mock(invitesApi.previewFails(aProblem().gone(message)))
      const join = joinPage(page)

      await join.openLink(token)

      await expect(join.at.alert(message)).toBeVisible()
      await expect(join.at.form).toHaveCount(0)
      await expect(join.at.toasts).toHaveCount(0)
    })
  }

  test('says that the page needs an invite link in a tab with no token, and asks the Server nothing', { tag: '@INV-3' }, async ({ page, api }) => {
    const join = joinPage(page)

    await join.openLinkWithNoPreview('/join')

    await expect(join.at.alert(linkNeeded)).toBeVisible()
    await expect(join.at.alert(notValid)).toHaveCount(0)
    await expect(join.at.homeLink).toBeVisible()
    expect(api.all('post', invitesPaths.preview)).toEqual([])
  })

  test('says that the link is not valid when its token is too long for the Server', { tag: '@INV-3' }, async ({ page, api }) => {
    const join = joinPage(page)

    await join.openLinkWithNoPreview(`/join#token=${'a'.repeat(44)}`)

    await expect(join.at.alert(notValid)).toBeVisible()
    expect(api.all('post', invitesPaths.preview)).toEqual([])
  })

  test('offers a retry when the preview fails for a reason that a retry can cure', { tag: '@INV-3' }, async ({ page, api }) => {
    api.mock(invitesApi.previewFails(aProblem().saying('The server had a problem.')))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.alert('The invite did not load.')).toBeVisible()
    await expect(join.at.toasts).toHaveCount(0)

    api.mock(invitesApi.previewIs(aPreview()))
    await join.at.retry.click()

    await expect(join.at.form).toBeVisible()
  })

  test('fills the form from the claims, and sends what the user changes', { tag: '@INV-8' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()), invitesApi.claims(aUser()))
    const join = joinPage(page)
    await join.openLink(token)

    await expect(join.at.firstName).toHaveValue('Ada')
    await expect(join.at.lastName).toHaveValue('Lovelace')
    await expect(join.at.email).toHaveValue('ada@example.com')
    await expect(join.at.username).toHaveValue('ada')

    await join.fill({ firstName: 'Augusta', lastName: 'King', email: 'augusta@example.com', username: 'augusta' })
    api.mock(membershipApi.userIsNow(aUser().identifiedBy(actorSubject)))
    await join.claimAndGoHome()

    expect(api.last('post', invitesPaths.claim)?.body).toEqual({
      token,
      firstName: 'Augusta',
      lastName: 'King',
      email: 'augusta@example.com',
      username: 'augusta',
    })
  })

  test('asks for each value before the claim, and moves the focus to the first gap', { tag: '@INV-8' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()))
    const join = joinPage(page)
    await join.openLink(token)
    await join.fill({ firstName: '', lastName: '', email: '', username: '' })

    await join.at.submit.click()

    await expect(join.at.alert('Enter your first name.')).toBeVisible()
    await expect(join.at.alert('Enter your last name.')).toBeVisible()
    await expect(join.at.alert('Enter your email address.')).toBeVisible()
    await expect(join.at.alert('Enter a username.')).toBeVisible()
    await expect(join.at.firstName).toBeFocused()
    expect(api.all('post', invitesPaths.claim)).toEqual([])
  })

  test('joins the tenant, says it in one toast, goes home, and forgets the token', { tag: '@INV-8' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview({ tenantName: 'Acme' })), invitesApi.claims(aUser()))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()
    api.mock(membershipApi.userIsNow(aUser().identifiedBy(actorSubject)))

    await join.claimAndGoHome()

    await expect(join.at.toast('You joined Acme.')).toBeVisible()
    await expect(join.at.toasts).toHaveCount(1)
    await expect(join.at.homeHeading).toBeFocused()
    await expect(join.at.startTenantNotice).toHaveCount(0)
    expect(await join.storedToken()).toBeNull()
  })

  test('shows the email of the account as read-only on an invite with an email match', { tag: ['@INV-8', '@INV-9'] }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview({ requireEmailMatch: true })), invitesApi.claims(aUser()))
    const join = joinPage(page)
    await join.openLink(token)

    await expect(join.at.email).toHaveValue('ada@example.com')
    await expect(join.at.email).not.toBeEditable()
    await expect(join.at.emailNote).toBeVisible()
  })

  test('shows the refusal of an email match in the form, and keeps the values', { tag: ['@INV-8', '@INV-9'] }, async ({ page, api }) => {
    const message = 'This invite is for another email address. Sign in with the account of that email.'
    api.mock(invitesApi.previewIs(aPreview({ requireEmailMatch: true })), invitesApi.claimFails(aProblem().forbidden(message)))
    const join = joinPage(page)
    await join.openLink(token)
    await join.at.username.fill('augusta')

    await join.claim()

    await expect(join.at.formAlert).toHaveText(message)
    await expect(join.at.username).toHaveValue('augusta')
    await expect(join.at.toasts).toHaveCount(0)
    expect(api.all('post', membershipPaths.userOf(actorSubject))).toEqual([])
  })

  test('shows a field refusal of the Server on its field', { tag: '@INV-8' }, async ({ page, api }) => {
    const message = 'Enter a valid email address.'
    api.mock(invitesApi.previewIs(aPreview()), invitesApi.claimFails(aProblem().invalid({ email: [message] })))
    const join = joinPage(page)
    await join.openLink(token)

    await join.claim()

    await expect(join.at.alert(message)).toBeVisible()
    await expect(join.at.email).toBeFocused()
  })

  for (const [status, problem] of [
    ['409', aProblem().conflict(alreadyMember)],
    ['410', aProblem().gone(usedOrRevoked)],
  ] as const) {
    test(`takes a ${status} as a success when the first submit won the claim`, { tag: '@INV-8' }, async ({ page, api }) => {
      const tenant = aTenant().named('Acme')
      const joined = aUser().identifiedBy(actorSubject)
      api.mock(invitesApi.previewIs(aPreview({ tenantName: 'Acme' })), invitesApi.claimFails(problem), tenantsApi.oneIs(tenant))
      const join = joinPage(page)
      await join.openLink(token)
      await expect(join.at.form).toBeVisible()
      api.mock(membershipApi.userIsNow(joined))

      await join.claimAndCheckMembership('tnt_1')

      await expect(page).toHaveURL('/')
      await expect(join.at.toast('You joined Acme.')).toBeVisible()
      await expect(join.at.toasts).toHaveCount(1)
      expect(await join.storedToken()).toBeNull()
    })
  }

  test('takes a 409 as a refusal when the caller joined another tenant', { tag: '@INV-8' }, async ({ page, api }) => {
    api.mock(
      invitesApi.previewIs(aPreview({ tenantName: 'Acme' })),
      invitesApi.claimFails(aProblem().conflict(alreadyMember)),
      tenantsApi.oneIs(aTenant().named('Globex')),
    )
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()
    api.mock(membershipApi.userIsNow(aUser().identifiedBy(actorSubject)))

    await join.claimAndCheckMembership('tnt_1')

    await expect(join.at.alreadyMember).toBeVisible()
    await expect(join.at.form).toHaveCount(0)
    await expect(join.at.heading).toBeFocused()
    await expect(join.at.toasts).toHaveCount(0)
  })

  test('shows the message of the Server when a claim loses the invite, and no form', { tag: '@INV-8' }, async ({ page, api }) => {
    api.mock(invitesApi.previewIs(aPreview()), invitesApi.claimFails(aProblem().gone(usedOrRevoked)))
    const join = joinPage(page)
    await join.openLink(token)
    await expect(join.at.form).toBeVisible()

    await join.claimAndCheckMembership()

    await expect(join.at.alert(usedOrRevoked)).toBeVisible()
    await expect(join.at.form).toHaveCount(0)
    await expect(join.at.heading).toBeFocused()
    await expect(join.at.toasts).toHaveCount(0)
  })

  test('holds long names and emails in a narrow window', { tag: '@INV-3' }, async ({ page, api }) => {
    await page.setViewportSize({ width: 320, height: 640 })
    const tenantName = `Acme ${'Longname'.repeat(20)}`
    api.mock(invitesApi.previewIs(aPreview({ tenantName })))
    const join = joinPage(page)

    await join.openLink(token)

    await expect(join.at.detail(tenantName)).toBeVisible()
    expect(await join.pageOverflow()).toBe(0)
  })
})

test.describe('a member of a tenant opens an invite link', () => {
  test.use({ actor: as.user('Bob Smith') })

  test('gets the message of the Server and a link home, with no form and no preview', { tag: '@INV-6' }, async ({ page, api }) => {
    const join = joinPage(page)

    await join.openLinkWithNoPreview(`/join#token=${token}`)

    await expect(join.at.alreadyMember).toBeVisible()
    await expect(join.at.homeLink).toBeVisible()
    await expect(join.at.form).toHaveCount(0)
    await expect(page).toHaveURL('/join')
    expect(api.all('post', invitesPaths.preview)).toEqual([])
  })

  test('holds the token for this member only, so that the next person in the tab gets no invite', { tag: '@INV-3' }, async ({ page, api }) => {
    const join = joinPage(page)
    await join.openLinkWithNoPreview(`/join#token=${token}`)
    await expect(join.at.alreadyMember).toBeVisible()

    const ada = as.userWithNoTenant('Ada Lovelace', 'ada')
    api.mock(bffApi.userIs(ada), ...membershipApi.userIs(ada))
    await join.open('/join')

    await expect(join.at.alert(linkNeeded)).toBeVisible()
    expect(api.all('post', invitesPaths.preview)).toEqual([])
    expect(await join.storedToken()).toBeNull()
  })
})

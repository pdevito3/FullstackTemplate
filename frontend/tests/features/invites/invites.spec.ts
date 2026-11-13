import { invitesApi, invitesPaths } from '@tests/support/api/invites-api'
import { membershipApi } from '@tests/support/api/membership-api'
import { tenantsApi } from '@tests/support/api/tenants-api'
import { usersApi } from '@tests/support/api/users-api'
import { actorSubject, as } from '@tests/support/auth/actors'
import { anInvite } from '@tests/support/builders/invite-builder'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { aTenant } from '@tests/support/builders/tenant-builder'
import { aUser } from '@tests/support/builders/user-builder'
import { browserZone } from '@tests/support/clock'
import { expect, test } from '@tests/support/fixtures'
import { usersPage } from '../users/users.tasks'
import { usersWithInvites } from './invites.tasks'

const token = 'A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8S9t0U1v'

/** `inviteExpiry` of the builder in each zone of the suite. */
const expiryIn: Record<string, string> = {
  'Pacific/Kiritimati': 'Jan 16, 2099, 2:00 AM',
  'Pacific/Pago_Pago': 'Jan 15, 2099, 1:00 AM',
}

test.describe('the users page of a plain member', () => {
  test.use({ actor: as.user('Bob Smith') })

  test('has no invite button and no invite list, and asks for no invites', async ({ page, api }) => {
    api.mock(tenantsApi.oneIs(aTenant()), usersApi.listReturns([aUser().named('Grace Hopper')]))
    const users = usersWithInvites(page)

    await users.open()

    await expect(users.at.usersGrid).toBeVisible()
    await expect(users.at.inviteButton).toHaveCount(0)
    await expect(users.at.invitesHeading).toHaveCount(0)
    expect(api.all('get', invitesPaths.list)).toEqual([])
  })

  test('names the failed read of the tenant, says nothing about invites, and can try again', async ({ page, api }) => {
    api.mock(tenantsApi.oneFails('tnt_1', aProblem().saying('The server had a problem.')), usersApi.listReturns([aUser().named('Grace Hopper')]))
    const users = usersWithInvites(page)
    await users.open()

    await expect(users.at.tenantFailure).toBeVisible()
    await expect(users.at.loadFailure).toHaveCount(0)
    await expect(users.at.usersGrid).toBeVisible()

    api.mock(tenantsApi.oneIs(aTenant().seenByAdmin()), invitesApi.listReturns([]))
    await users.at.tenantRetry.click()

    await expect(users.at.inviteButton).toBeVisible()
    await expect(users.at.tenantFailure).toHaveCount(0)
  })
})

test.describe('the invites of a tenant admin', () => {
  test.use({ actor: as.user('Ana Admin') })

  test.beforeEach(({ api }) => {
    api.mock(tenantsApi.oneIs(aTenant().seenByAdmin()), usersApi.listReturns([aUser().named('Grace Hopper')]))
  })

  test('lists the role, the email, the state, and the expiry in the zone of the browser', { tag: '@zone' }, async ({ page, api }) => {
    api.mock(invitesApi.listReturns([
      anInvite().forEmail('pending@example.com').inRole('Admin'),
      anInvite().matchingEmail('match@example.com'),
      anInvite().forEmail('expired@example.com').expired(),
      anInvite().forEmail('claimed@example.com').claimed(),
      anInvite().forEmail('revoked@example.com').revoked(),
      anInvite(),
    ]))
    const users = usersWithInvites(page)

    await users.openWithList()

    await expect(users.at.cell('pending@example.com', 'Admin')).toBeVisible()
    await expect(users.at.cell('pending@example.com', 'Pending')).toBeVisible()
    await expect(users.at.cell('pending@example.com', expiryIn[browserZone()])).toBeVisible()
    await expect(users.at.rowEmailNote('match@example.com', 'Email must match')).toBeVisible()
    await expect(users.at.cell('expired@example.com', 'Expired')).toBeVisible()
    await expect(users.at.cell('claimed@example.com', 'Claimed')).toBeVisible()
    await expect(users.at.cell('revoked@example.com', 'Revoked')).toBeVisible()
    await expect(users.at.cell('Anyone with the link', 'Pending')).toBeVisible()
    await expect(users.at.revokeOf('pending@example.com')).toBeVisible()
    await expect(users.at.revokeOf('claimed@example.com')).toHaveCount(0)
    await expect(users.at.revokeOf('revoked@example.com')).toHaveCount(0)
  })

  test('tells that there are no invites', async ({ page, api }) => {
    api.mock(invitesApi.listReturns([]))
    const users = usersWithInvites(page)

    await users.openWithList()

    await expect(users.at.empty).toBeVisible()
  })

  test('puts the page of the invites in the URL, and sends it to the API', async ({ page, api }) => {
    api.mock(invitesApi.listReturns([anInvite().forEmail('first@example.com')], { total: 25 }))
    const users = usersWithInvites(page)
    await users.openWithList()

    api.mock(invitesApi.listReturns([anInvite().forEmail('second@example.com')], { total: 25, pageNumber: 2 }))
    await users.goToNextPage()

    await expect(page).toHaveURL('/users?invitesPage=2')
    await expect(users.at.row('second@example.com')).toBeVisible()
    expect(api.last('get', invitesPaths.list)?.query.get('pageNumber')).toBe('2')
  })

  test('shows the failure of the list', async ({ page, api }) => {
    api.mock(invitesApi.listFails(aProblem().saying('The server had a problem.')))
    const users = usersWithInvites(page)

    await users.openWithList()

    await expect(users.at.loadFailure).toBeVisible()
  })

  test('creates an invite, and shows the link in its fragment with the only-time sentence', { tag: ['@INV-3', '@zone'] }, async ({ page, api, context, baseURL }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    const invite = anInvite().inRole('Admin').forEmail('new@example.com')
    api.mock(invitesApi.listReturns([]), invitesApi.creates(invite, token))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()
    await users.pickRole('Admin')
    await users.at.emailField.fill('new@example.com')
    await users.at.emailMatch.click()

    api.mock(invitesApi.listReturns([invite]))
    await users.createInvite()

    expect(api.last('post', invitesPaths.list)?.body).toEqual({ role: 'Admin', email: 'new@example.com', requireEmailMatch: true })
    const link = `${new URL(baseURL!).origin}/join#token=${token}`
    await expect(users.at.linkField).toHaveValue(link)
    await expect(users.at.onlyTimeSentence).toBeVisible()
    await expect(users.at.linkExpiry).toContainText(expiryIn[browserZone()])
    await expect(users.at.copyLink).toBeFocused()
    expect(await users.copyTheLink()).toBe(link)
    await expect(page).toHaveURL('/users')
    await expect(users.at.toasts).toHaveCount(0)
    await expect(users.at.row('new@example.com')).toBeVisible()
  })

  test('keeps the link on Escape, closes on Done, and gives a new form the next time', async ({ page, api }) => {
    api.mock(invitesApi.listReturns([]), invitesApi.creates(anInvite(), token))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()
    await users.createInvite()

    await users.pressEscape()
    await expect(users.at.linkDialog).toBeVisible()

    await users.closeLinkDialog()
    await expect(users.at.inviteButton).toBeFocused()

    await users.openDialog()
    await expect(users.at.createLink).toBeVisible()
    await expect(users.at.linkField).toHaveCount(0)
  })

  test('gives a new form after a cancel', async ({ page, api }) => {
    api.mock(invitesApi.listReturns([]))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()
    await users.at.emailField.fill('new@example.com')

    await users.cancelDialog()
    await expect(users.at.inviteButton).toBeFocused()
    await users.openDialog()

    await expect(users.at.emailField).toHaveValue('')
  })

  test('creates one invite when the user presses Enter twice, and shows the link of that invite', { tag: '@INV-2' }, async ({ page, api }) => {
    api.mock(invitesApi.listReturns([]), invitesApi.creates(anInvite(), token))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()
    await users.at.emailField.fill('new@example.com')

    await users.pressEnterTwiceInTheEmailField()

    await expect(users.at.linkField).toBeVisible()
    expect(api.all('post', invitesPaths.list)).toHaveLength(1)
  })

  test('creates one invite when the second Enter comes before a render of the first', { tag: '@INV-2' }, async ({ page, api }) => {
    api.mock(invitesApi.listReturns([]), { ...invitesApi.creates(anInvite(), token), delayMs: 300 })
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()
    await users.at.emailField.fill('new@example.com')

    await users.pressEnterTwiceBeforeARender()

    await expect(users.at.linkField).toBeVisible()
    expect(api.all('post', invitesPaths.list)).toHaveLength(1)
  })

  test('cannot close the dialog while the create runs, shows the link, and gives a new form the next time', { tag: '@INV-2' }, async ({ page, api, baseURL }) => {
    api.mock(invitesApi.listReturns([]), invitesApi.creates(anInvite(), token))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()
    await users.at.emailField.fill('new@example.com')

    await users.whileTheCreateRuns(async () => {
      await users.at.createLink.click()

      await expect(users.at.cancelInvite).toBeDisabled()
      await expect(users.at.closeInvite).toHaveCount(0)
      await page.keyboard.press('Escape')
      await expect(users.at.cancelInvite).toBeDisabled()
    })

    await expect(users.at.linkField).toHaveValue(`${new URL(baseURL!).origin}/join#token=${token}`)

    await users.closeLinkDialog()
    await users.openDialog()

    await expect(users.at.emailField).toHaveValue('')
    await expect(users.at.linkField).toHaveCount(0)
    await users.cancelDialog()
    await expect(users.at.inviteButton).toBeFocused()
  })

  test('shows the link before the list has its new answer', { tag: '@INV-2' }, async ({ page, api }) => {
    api.mock(invitesApi.listReturns([]), invitesApi.creates(anInvite(), token))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()
    api.mock({ ...invitesApi.listReturns([anInvite()]), delayMs: 3000 })

    await users.createInviteAndSeeTheLinkBeforeTheList()

    await expect(users.at.linkField).toBeVisible()
  })

  test('keeps the button and the list when a background read of the tenant fails', async ({ page, api }) => {
    api.mock(invitesApi.listReturns([]))
    const users = usersWithInvites(page)
    await users.openWithList()

    api.mock(tenantsApi.oneFails('tnt_1', aProblem().saying('The server had a problem.')))
    await users.readTheTenantAgainAfterAReconnect()

    await expect(users.at.inviteButton).toBeVisible()
    await expect(users.at.tenantFailure).toHaveCount(0)
  })

  test('shows no button and asks for no invites after the admin demotes themselves and comes back', async ({ page, api }) => {
    const ana = aUser().identifiedBy(actorSubject).named('Ana Admin').inRole('Admin').seenByAdmin()
    const { id } = ana.build()
    api.mock(usersApi.listReturns([ana]), usersApi.oneIs(ana), membershipApi.userIsNow(ana), invitesApi.listReturns([]))
    const users = usersWithInvites(page)
    const people = usersPage(page)
    await users.openWithList()
    await users.openUserFromTheList('Ana Admin')
    await people.pickRole('Ana Admin', 'User')

    const demoted = ana.inRole('User').allowing()
    api.mock(
      usersApi.roleChanges(demoted),
      usersApi.oneIs(demoted),
      membershipApi.userIsNow(demoted),
      { ...tenantsApi.oneIs(aTenant()), delayMs: 300 },
      invitesApi.listFails(aProblem().forbidden('You cannot manage users.')),
    )
    await people.confirmRole(id)
    const invitesReads = api.all('get', invitesPaths.list).length

    expect(await users.goToUsersAndCountTheButtonsBeforeTheTenantAnswers()).toBe(0)

    await expect(users.at.usersGrid).toBeVisible()
    await expect(users.at.inviteButton).toHaveCount(0)
    await expect(users.at.invitesHeading).toHaveCount(0)
    await expect(users.at.toasts).toHaveCount(0)
    expect(api.all('get', invitesPaths.list)).toHaveLength(invitesReads)
  })

  test('sends no email when the field is empty', async ({ page, api }) => {
    api.mock(invitesApi.listReturns([]), invitesApi.creates(anInvite(), token))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()

    await users.createInvite()

    expect(api.last('post', invitesPaths.list)?.body).toEqual({ role: 'User', email: null, requireEmailMatch: false })
  })

  test('shows the refusal of an email match with no email on the email field', async ({ page, api }) => {
    const message = 'Please give the email that the invite is for, or do not require an email match.'
    api.mock(invitesApi.listReturns([]), invitesApi.createFails(aProblem().invalid({ email: [message] })))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()
    await users.at.emailMatch.click()

    await users.submitDialog()

    await expect(users.at.fieldError(message)).toBeVisible()
    await expect(users.at.emailField).toBeFocused()
    await expect(users.at.toasts).toHaveCount(0)
    await expect(users.at.linkField).toHaveCount(0)
  })

  test('holds a long link in the dialog of a narrow window', async ({ page, api }) => {
    await page.setViewportSize({ width: 320, height: 640 })
    api.mock(invitesApi.listReturns([]), invitesApi.creates(anInvite(), token))
    const users = usersWithInvites(page)
    await users.openWithList()
    await users.openDialog()

    await users.createInvite()

    await expect(users.at.linkField).toBeVisible()
    expect(await users.linkFieldOverflow()).toBe(0)
    expect(await users.pageOverflow()).toBe(0)
  })

  test('holds a long email in the list of a narrow window', async ({ page, api }) => {
    await page.setViewportSize({ width: 320, height: 640 })
    const email = `${'long'.repeat(25)}@example.com`
    api.mock(invitesApi.listReturns([anInvite().forEmail(email)]))
    const users = usersWithInvites(page)

    await users.openWithList()

    await expect(users.at.row(email)).toBeVisible()
    expect(await users.pageOverflow()).toBe(0)
  })

  test('asks before it revokes, and moves the focus to the heading of the list', { tag: '@INV-10' }, async ({ page, api }) => {
    const invite = anInvite().forEmail('old@example.com')
    const { id } = invite.build()
    api.mock(invitesApi.listReturns([invite]), invitesApi.revokes(invite.revoked()))
    const users = usersWithInvites(page)
    await users.openWithList()

    await users.askToRevoke('old@example.com')
    await users.keepInvite()
    expect(api.all('post', invitesPaths.revoke(id))).toEqual([])

    await users.askToRevoke('old@example.com')
    api.mock(invitesApi.listReturns([invite.revoked()]))
    await users.confirmRevoke(id)

    await expect(users.at.cell('old@example.com', 'Revoked')).toBeVisible()
    await expect(users.at.toast('Invite revoked')).toBeVisible()
    await expect(users.at.toasts).toHaveCount(1)
    await expect(users.at.invitesHeading).toBeFocused()
  })

  test('shows the refusal of a revoke in one toast, and reads the list again', { tag: '@INV-10' }, async ({ page, api }) => {
    const message = 'This invite was used or revoked. Ask a tenant admin for a new invite.'
    const invite = anInvite().forEmail('old@example.com')
    const { id } = invite.build()
    api.mock(invitesApi.listReturns([invite]), invitesApi.revokeFails(id, aProblem().gone(message)))
    const users = usersWithInvites(page)
    await users.openWithList()

    await users.askToRevoke('old@example.com')
    api.mock(invitesApi.listReturns([invite.claimed()]))
    await users.confirmRevoke(id)

    await expect(users.at.toast(message)).toBeVisible()
    await expect(users.at.toasts).toHaveCount(1)
    await expect(users.at.cell('old@example.com', 'Claimed')).toBeVisible()
    await expect(users.at.invitesHeading).toBeFocused()
  })
})

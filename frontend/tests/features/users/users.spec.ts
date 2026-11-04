import { usersApi, usersPaths } from '@tests/support/api/users-api'
import { as } from '@tests/support/auth/actors'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { aUser } from '@tests/support/builders/user-builder'
import { expect, test } from '@tests/support/fixtures'
import { usersPage } from './users.tasks'

test.describe('the users of a tenant', () => {
  test.use({ actor: as.user('Ana Admin') })

  test('lists the users of the tenant with their roles, and opens a user from the list', { tag: '@UM-1' }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper').seenByAdmin()
    const alan = aUser().named('Alan Turing').inRole('Admin').seenByAdmin()
    const { id } = grace.build()
    api.mock(usersApi.listReturns([grace, alan]), usersApi.oneIs(grace))
    const users = usersPage(page)

    await users.openList()

    await expect(users.at.cell('Grace Hopper', 'grace@example.com')).toBeVisible()
    await expect(users.at.cell('Grace Hopper', 'User')).toBeVisible()
    await expect(users.at.cell('Alan Turing', 'Admin')).toBeVisible()
    await expect(users.at.usersNavLink).toHaveAttribute('aria-current', 'page')

    await users.openUser('Grace Hopper', id)

    await expect(users.at.detail('grace@example.com')).toBeVisible()
    await expect(users.at.detail('grace')).toBeVisible()
    await expect(users.at.detail('User')).toBeVisible()
    await expect(users.at.usersNavLink).toHaveAttribute('aria-current', 'true')
  })

  test('puts the page in the URL, and sends it to the API', { tag: '@UM-1' }, async ({ page, api }) => {
    api.mock(usersApi.listReturns([aUser().named('Grace Hopper')], { total: 25 }))
    const users = usersPage(page)
    await users.openList()

    api.mock(usersApi.listReturns([aUser().named('Alan Turing')], { total: 25, pageNumber: 2 }))
    await users.goToNextPage()

    await expect(page).toHaveURL('/users?page=2')
    await expect(users.at.row('Alan Turing')).toBeVisible()
    expect(api.last('get', usersPaths.list)?.query.get('pageNumber')).toBe('2')
    expect(api.last('get', usersPaths.list)?.query.get('pageSize')).toBe('10')
  })

  test('shows an empty page past the end of the list, with a link to the first page', { tag: '@UM-1' }, async ({ page, api }) => {
    api.mock(usersApi.listReturns([], { total: 25, pageNumber: 5 }))
    const users = usersPage(page)
    await users.openListAt('/users?page=5')

    await expect(users.at.emptyPage).toContainText('There are no users on page 5.')

    api.mock(usersApi.listReturns([aUser().named('Grace Hopper')], { total: 25 }))
    await users.goToFirstPage()

    await expect(page).toHaveURL('/users')
    await expect(users.at.row('Grace Hopper')).toBeVisible()
  })

  test('changes the role that the caller picks, and moves the focus to the heading', { tag: '@UM-2' }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper').seenByAdmin()
    const { id } = grace.build()
    api.mock(usersApi.oneIs(grace))
    const users = usersPage(page)
    await users.openDetail(id)

    await users.pickRole('Grace Hopper', 'Admin')
    await users.keepRole()

    expect(api.all('put', usersPaths.role(id))).toHaveLength(0)
    await expect(users.at.detail('User')).toBeVisible()

    api.mock(usersApi.roleChanges(grace.inRole('Admin').savedAgain()))
    await users.pickRole('Grace Hopper', 'Admin')
    await users.confirmRole(id)

    expect(api.last('put', usersPaths.role(id))?.body).toEqual({ role: 'Admin' })
    await expect(users.at.roleDialog).toBeHidden()
    await expect(users.at.detail('Admin')).toBeVisible()
    await expect(users.at.detailHeading('Grace Hopper')).toBeFocused()
  })

  test('a refused role change reads the user again, and shows only the actions that the Server now allows', { tag: '@UM-2' }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper').seenByAdmin()
    const { id } = grace.build()
    api.mock(usersApi.oneIs(grace))
    const users = usersPage(page)
    await users.openDetail(id)
    api.mock(
      usersApi.roleChangeFails(id, aProblem().forbidden('You cannot manage the access of users. Ask a tenant admin for access.')),
      usersApi.oneIs(grace.allowing('Delete')),
    )

    await users.pickRole('Grace Hopper', 'Admin')
    await users.confirmRoleRefused(id)

    await expect(users.at.toast('You cannot manage the access of users. Ask a tenant admin for access.')).toBeVisible()
    await expect(users.at.actionButton('Grace Hopper', 'Change role')).toHaveCount(0)
    await expect(users.at.actionButton('Grace Hopper', 'Delete')).toBeVisible()
    await expect(users.at.detail('User')).toBeVisible()
    await expect(users.at.detailHeading('Grace Hopper')).toBeFocused()
  })

  test('removes a user after the caller confirms, and opens the list with one toast', { tag: '@UM-3' }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper').seenByAdmin()
    const alan = aUser().named('Alan Turing')
    const { id } = grace.build()
    api.mock(usersApi.oneIs(grace))
    const users = usersPage(page)
    await users.openDetail(id)

    await users.askToDelete('Grace Hopper')
    await users.keepUser()

    expect(api.all('delete', usersPaths.one(id))).toHaveLength(0)
    await expect(users.at.actionButton('Grace Hopper', 'Delete')).toBeFocused()

    api.mock(usersApi.deletes(id), usersApi.listReturns([alan]))
    await users.askToDelete('Grace Hopper')
    await users.confirmDeleteAndOpenList(id)

    await expect(page).toHaveURL('/users')
    await expect(users.at.toast('User deleted')).toBeVisible()
    expect(await users.at.toasts.count()).toBe(1)
    await expect(users.at.listHeading).toBeFocused()
    await expect(users.at.row('Alan Turing')).toBeVisible()
    await expect(users.at.row('Grace Hopper')).toHaveCount(0)
    expect(api.all('get', usersPaths.one(id))).toHaveLength(1)
  })

  test('a refused delete keeps the user on the page, with the focus on the heading', { tag: '@UM-3' }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper').seenByAdmin()
    const { id } = grace.build()
    api.mock(usersApi.oneIs(grace), usersApi.deleteFails(id, aProblem().forbidden('You cannot manage users. Ask a tenant admin for access.')))
    const users = usersPage(page)
    await users.openDetail(id)

    await users.askToDelete('Grace Hopper')
    await users.confirmDeleteRefused(id)

    await expect(users.at.toast('You cannot manage users. Ask a tenant admin for access.')).toBeVisible()
    await expect(page).toHaveURL(`/users/${id}`)
    await expect(users.at.detailHeading('Grace Hopper')).toBeFocused()
  })

  test('shows Change role and Delete to an admin, and no button for an action that no page sends', { tag: ['@UM-2', '@UM-3'] }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper').seenByAdmin()
    api.mock(usersApi.oneIs(grace))
    const users = usersPage(page)

    await users.openDetail(grace.build().id)

    await expect(users.at.actionButton('Grace Hopper', 'Change role')).toBeVisible()
    await expect(users.at.actionButton('Grace Hopper', 'Delete')).toBeVisible()
    expect(await users.at.actionButtons('Grace Hopper').count()).toBe(2)
  })

  test('shows no Change role to a caller who cannot change the role', { tag: '@UM-2' }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper').allowing('Delete')
    api.mock(usersApi.oneIs(grace))
    const users = usersPage(page)

    await users.openDetail(grace.build().id)

    await expect(users.at.actionButton('Grace Hopper', 'Delete')).toBeVisible()
    await expect(users.at.actionButton('Grace Hopper', 'Change role')).toHaveCount(0)
  })

  test('shows no Delete to a caller who cannot remove the user', { tag: '@UM-3' }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper').allowing('UpdateRole')
    api.mock(usersApi.oneIs(grace))
    const users = usersPage(page)

    await users.openDetail(grace.build().id)

    await expect(users.at.actionButton('Grace Hopper', 'Change role')).toBeVisible()
    await expect(users.at.actionButton('Grace Hopper', 'Delete')).toHaveCount(0)
  })

  test('shows no actions to a caller whom the Server allows none', { tag: ['@UM-2', '@UM-3'] }, async ({ page, api }) => {
    const grace = aUser().named('Grace Hopper')
    api.mock(usersApi.oneIs(grace))
    const users = usersPage(page)

    await users.openDetail(grace.build().id)

    await expect(users.at.detailHeading('Grace Hopper')).toBeVisible()
    await expect(users.at.actions('Grace Hopper')).toHaveCount(0)
  })
})

test.describe('the users of a tenant, for a member who cannot see them', () => {
  test.use({ actor: as.user('Bob Smith') })

  test('shows the refusal of the Server in place of the list', { tag: '@UM-1' }, async ({ page, api }) => {
    api.mock(usersApi.listFails(aProblem().forbidden('You cannot see the users of the tenant. Ask a tenant admin for access.')))
    const users = usersPage(page)

    await users.openList()

    await expect(users.at.loadFailure).toContainText('You cannot see the users of the tenant. Ask a tenant admin for access.')
    await expect(users.at.grid).toHaveCount(0)
  })
})

test.describe('the users of a signed-in user with no tenant', () => {
  test.use({ actor: as.userWithNoTenant('Ada Lovelace') })

  test('shows the way to start a tenant in place of the list, and asks for no list', { tag: '@UM-1' }, async ({ page, api }) => {
    api.mock(usersApi.listReturns([]))
    const users = usersPage(page)

    await page.goto('/users')

    await expect(users.at.startTenantFirst).toBeVisible()
    await expect(users.at.startTenantLink).toHaveAttribute('href', '/start-tenant')
    expect(api.last('get', usersPaths.list)).toBeUndefined()
  })
})

const longName = 'Maximilianbartholomewalexandervanderwesthuizenmontgomerysmythesinclairfitzgeraldworthingtonhughes'

test.describe('the users pages on a 320 px phone', () => {
  test.use({ actor: as.user('Ana Admin'), viewport: { width: 320, height: 640 } })

  test('the list shows the name, the email, and the role of each row with no sideways scroll', { tag: '@UM-1' }, async ({ page, api }) => {
    api.mock(usersApi.listReturns([aUser().named('Grace Hopper'), aUser().named(`${longName} Smith`)]))
    const users = usersPage(page)

    await users.openList()

    await expect(users.at.row('Grace Hopper')).toContainText('grace@example.com')
    expect(await users.sidewaysOverflowOfTable()).toBe(0)
    expect(await users.pageOverflow()).toBe(0)
  })

  test('the detail wraps a long name and a long email in the window', async ({ page, api }) => {
    const user = aUser().named(`${longName} Smith`).seenByAdmin()
    api.mock(usersApi.oneIs(user))
    const users = usersPage(page)

    await users.openDetail(user.build().id)

    await expect(users.at.detailHeading(`${longName} Smith`)).toBeVisible()
    await expect(users.at.detail(`${longName.toLowerCase()}@example.com`)).toBeVisible()
    expect(await users.pageOverflow()).toBe(0)
  })
})

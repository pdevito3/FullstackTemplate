import { bffApi, bffPaths } from '@tests/support/api/bff-api'
import { as } from '@tests/support/auth/actors'
import { usersPaths } from '@tests/support/api/users-api'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { expect, test } from '@tests/support/fixtures'
import { blankPage, sessionPage } from './session.tasks'

test.describe('a visitor with no session', () => {
  test.use({ actor: as.anonymous() })

  test('goes to the login at once, with the deep link, its query, and its hash as the return URL', async ({ page, api }) => {
    api.mock(bffApi.loginOpens())

    const { rendered } = await sessionPage(page).openAndGoToLogin('/about?tab=team&x=1#people')

    expect(api.last('get', bffPaths.login)?.query.get('returnUrl')).toBe('/about?tab=team&x=1#people')
    expect(rendered, 'the app rendered a page before it went to the login').toBe(false)
  })

  test('replaces the deep link in the history, so Back from the login opens the page before it', async ({ page, api }) => {
    api.mock(bffApi.loginOpens())
    const session = sessionPage(page)
    await session.openBlankPage()

    await session.openDeepLinkAndWaitForLogin('/about')
    await page.goBack()

    expect(page.url()).toBe(blankPage)
  })
})

test.describe('a session that ends while the page is open', () => {
  test('goes to the login at the next call of the API, with the page as the return URL, and shows no toast', async ({ page, api }) => {
    api.mock(bffApi.loginOpens())
    const session = sessionPage(page)
    await session.open('/about')

    api.mock(...bffApi.sessionEnded(usersPaths.current))
    const { toasted } = await session.followAndGoToLogin(session.at.homeLink)

    expect(api.last('get', bffPaths.login)?.query.get('returnUrl')).toBe('/')
    expect(toasted, 'a toast showed before the login').toBe(false)
  })
})

test.describe('a call of the API that answers 401 while the session lives', () => {
  test('keeps the page, does not go to the login, and shows a toast', async ({ page, api }) => {
    const session = sessionPage(page)
    await session.open('/about')

    api.mock(...bffApi.apiRejectsTheToken(usersPaths.current))
    await session.followAndWaitForTheSessionCheck(session.at.homeLink)

    await expect(session.at.rejectedSignInToast).toBeVisible()
    await expect(session.at.navigation).toBeVisible()
    expect(api.all('get', bffPaths.login)).toHaveLength(0)
  })
})

test.describe('a failed check of the session', () => {
  test('shows an alert and no page or toast, and does not go to the login', async ({ page, api }) => {
    api.mock(bffApi.userFails(aProblem().saying('The session store is down.')))

    const session = sessionPage(page)
    await session.openWhileTheCheckFails('/about')

    await expect(session.at.navigation).toHaveCount(0)
    await expect(session.at.aboutHeading).toHaveCount(0)
    // The toast shows with the failed request, before the alert. A toast closes after 6 seconds, so read the count once.
    expect(await session.at.toasts.count()).toBe(0)
    expect(api.all('get', bffPaths.login)).toHaveLength(0)
  })

  test('shows the alert when the check answers 401', async ({ page, api }) => {
    api.mock({ method: 'get', path: bffPaths.user, status: 401 })

    const session = sessionPage(page)
    await session.openWhileTheCheckFails('/about')

    expect(api.all('get', bffPaths.login)).toHaveLength(0)
  })

  test('shows the page after Try Again', async ({ page, api }) => {
    api.mock(bffApi.userFails(aProblem().saying('The session store is down.')))
    const session = sessionPage(page)
    await session.openWhileTheCheckFails('/about')

    api.mock(bffApi.userIs(as.user()))
    await session.tryAgain()

    await expect(session.at.aboutHeading).toBeVisible()
    await expect(session.at.checkFailed).toHaveCount(0)
  })

  test('shows Try Again as pending while the check runs again, and sends one check for two presses', async ({ page, api }) => {
    api.mock(bffApi.userFails(aProblem().saying('The session store is down.')))
    const session = sessionPage(page)
    await session.openWhileTheCheckFails('/about')
    const checksBefore = api.all('get', bffPaths.user).length

    api.mock({ ...bffApi.userIs(as.user()), delayMs: 1000 })
    await session.pressTryAgain()
    await expect(session.at.tryAgain).toBeDisabled()
    await session.pressTryAgain()

    await expect(session.at.aboutHeading).toBeVisible()
    expect(api.all('get', bffPaths.user)).toHaveLength(checksBefore + 1)
  })
})

test.describe('a login that the BFF refused', () => {
  test.use({ actor: as.anonymous() })

  test('shows the sign-in failure with no layout, stays there, and signs in again with the home page as the return URL', async ({ page, api }) => {
    api.mock(bffApi.loginOpens())
    const session = sessionPage(page)

    await session.openFailurePage('/sign-in-failed', session.at.signInFailedHeading)

    await expect(session.at.navigation).toHaveCount(0)
    expect(api.all('get', bffPaths.login)).toHaveLength(0)
    await session.pressAndLeaveFor(session.at.signInAgain, bffPaths.login)
    expect(api.last('get', bffPaths.login)?.query.get('returnUrl')).toBe('/')
  })
})

test.describe('a logout that the BFF refused', () => {
  test('shows the sign-out failure with no layout, and signs out again with the sid of the session', async ({ page, api }) => {
    api.mock(bffApi.logoutOpens())
    const session = sessionPage(page)

    await session.openFailurePage('/sign-out-failed', session.at.signOutFailedHeading)

    await expect(session.at.navigation).toHaveCount(0)
    await session.pressAndLeaveFor(session.at.signOutAgain, bffPaths.logout)
    expect(api.last('get', bffPaths.logout)?.query.get('sid')).toBe('test-session')
  })

  test.describe('with no session', () => {
    test.use({ actor: as.anonymous() })

    test('signs out again with no sid', async ({ page, api }) => {
      api.mock(bffApi.logoutOpens())
      const session = sessionPage(page)

      await session.openFailurePage('/sign-out-failed', session.at.signOutFailedHeading)

      await session.pressAndLeaveFor(session.at.signOutAgain, bffPaths.logout)
      expect(api.last('get', bffPaths.logout)?.query.has('sid')).toBe(false)
    })
  })
})

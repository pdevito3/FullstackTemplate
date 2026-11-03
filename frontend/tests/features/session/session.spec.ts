import { bffApi, bffPaths } from '@tests/support/api/bff-api'
import { as } from '@tests/support/auth/actors'
import { usersPaths } from '@tests/support/api/users-api'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { expect, test } from '@tests/support/fixtures'
import { sessionPage } from './session.tasks'

test.describe('a visitor with no session', () => {
  test.use({ actor: as.anonymous() })

  test('goes to the login at once, with the deep link, its query, and its hash as the return URL', async ({ page, api }) => {
    api.mock(bffApi.loginOpens())

    const { rendered } = await sessionPage(page).openAndGoToLogin('/about?tab=team&x=1#people')

    expect(api.last('get', bffPaths.login)?.query.get('returnUrl')).toBe('/about?tab=team&x=1#people')
    expect(rendered, 'the app rendered a page before it went to the login').toBe(false)
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

  test('shows the page after Try Again', async ({ page, api }) => {
    api.mock(bffApi.userFails(aProblem().saying('The session store is down.')))
    const session = sessionPage(page)
    await session.openWhileTheCheckFails('/about')

    api.mock(bffApi.userIs(as.user()))
    await session.tryAgain()

    await expect(session.at.aboutHeading).toBeVisible()
    await expect(session.at.checkFailed).toHaveCount(0)
  })
})

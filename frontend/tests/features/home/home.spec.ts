import { bffApi, bffPaths } from '@tests/support/api/bff-api'
import { usersApi } from '@tests/support/api/users-api'
import { as } from '@tests/support/auth/actors'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { expect, test } from '@tests/support/fixtures'
import { homePage } from './home.tasks'

test.describe('a signed-in user on the home page', () => {
  test.use({ actor: as.user('Bob Smith') })

  test('sees the name that the Server reads from the access token', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: '1', name: 'Robert Smith' }))
    const home = homePage(page)

    await home.open()

    await expect(home.at.signedInAs('Robert Smith')).toBeVisible()
  })

  test('sees the identifier when the token has no name', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: 'user-1', name: null }))
    const home = homePage(page)

    await home.open()

    await expect(home.at.signedInAs('user-1')).toBeVisible()
  })

  test('sees an error when the Server fails to load the user', async ({ page, api }) => {
    api.mock(usersApi.currentFails(aProblem().saying('The Server is down.')))
    const home = homePage(page)

    await home.open()

    await expect(home.at.userFailed).toBeVisible()
  })

  test('goes to the logout of the session', async ({ page, api }) => {
    api.mock(usersApi.currentIs({ identifier: '1', name: 'Robert Smith' }), bffApi.logoutOpens())
    const home = homePage(page)
    await home.open()

    await home.logOut()

    expect(api.last('get', bffPaths.logout)?.query.get('sid')).toBe('test-session')
  })
})

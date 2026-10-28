import { bffApi, bffPaths } from '@tests/support/api/bff-api'
import { weatherApi, weatherPaths } from '@tests/support/api/weather-api'
import { as } from '@tests/support/auth/actors'
import { aForecast } from '@tests/support/builders/forecast-builder'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { expect, test } from '@tests/support/fixtures'
import { weatherForecastPage } from './weather-forecast.tasks'

test.describe('the weather forecast', () => {
  test('lists the days that the API returns, in Fahrenheit', async ({ page, api }) => {
    const warm = aForecast().on('2026-03-14').summarized('Warm').at(25)
    api.mock(weatherApi.listReturns(warm, aForecast().on('2026-03-15').summarized('Chilly')))

    const weather = weatherForecastPage(page)
    await weather.open()

    await expect(weather.at.days).toHaveCount(2)
    await expect(weather.at.day('Sat, Mar 14')).toContainText('Warm')
    await expect(weather.at.day('Sun, Mar 15')).toContainText('Chilly')
    await expect(weather.at.temperature('Sat, Mar 14')).toHaveText(`${warm.build().temperatureF}°`)
    await expect(weather.at.scale('Sat, Mar 14')).toHaveText('Fahrenheit')
  })

  test('shows the temperatures in Celsius after the switch', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast().on('2026-03-14').at(25)))
    const weather = weatherForecastPage(page)
    await weather.open()

    await weather.showInCelsius()

    await expect(weather.at.temperature('Sat, Mar 14')).toHaveText('25°')
    await expect(weather.at.scale('Sat, Mar 14')).toHaveText('Celsius')
  })

  test('sends the CSRF header that the BFF requires', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()))

    await weatherForecastPage(page).open()

    expect(api.last('get', weatherPaths.list)?.headers.get('x-csrf')).toBe('1')
  })

  test('shows the latest forecast after a refresh', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast().on('2026-03-14').summarized('Cool')))
    const weather = weatherForecastPage(page)
    await weather.open()

    api.mock(weatherApi.listReturns(aForecast().on('2026-03-14').summarized('Hot')))
    await weather.refresh()

    await expect(weather.at.day('Sat, Mar 14')).toContainText('Hot')
    expect(api.all('get', weatherPaths.list)).toHaveLength(2)
  })

  test('says so when the API has no forecast, and loads it again on Try Again', async ({ page, api }) => {
    api.mock(weatherApi.listReturns())
    const weather = weatherForecastPage(page)
    await weather.open()
    await expect(weather.at.emptyMessage).toBeVisible()

    api.mock(weatherApi.listReturns(aForecast().on('2026-03-14')))
    await weather.tryAgain()

    await expect(weather.at.day('Sat, Mar 14')).toBeVisible()
    await expect(weather.at.emptyMessage).toHaveCount(0)
  })

  // The query retries once, and the page shows the error only after the last attempt. By then each attempt has
  // shown its toast. The count is read once: `toHaveCount(1)` retries, and it passes when the first toast closes.
  test('shows the detail of an API error on the page and in one toast', async ({ page, api }) => {
    api.mock(weatherApi.listFails(aProblem().saying('The weather service is down.')))

    const weather = weatherForecastPage(page)
    await weather.open()

    await expect(weather.at.error).toContainText('The weather service is down.')
    await expect(weather.at.emptyMessage).toHaveCount(0)
    expect(api.all('get', weatherPaths.list)).toHaveLength(2)
    expect(await weather.at.toast('The weather service is down.').count()).toBe(1)
  })

  test('keeps the last forecast under the error when a refresh fails', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast().on('2026-03-14').summarized('Cool')))
    const weather = weatherForecastPage(page)
    await weather.open()

    api.mock(weatherApi.listFails(aProblem().saying('The weather service is down.')))
    await weather.refresh()

    await expect(weather.at.error).toContainText('The weather service is down.')
    await expect(weather.at.day('Sat, Mar 14')).toContainText('Cool')
  })
})

test.describe('an anonymous visitor', () => {
  test('sees the login and no secure API', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()))

    const weather = weatherForecastPage(page)
    await weather.open()

    await expect(weather.at.login).toBeVisible()
    await expect(weather.at.secureToggle).toHaveCount(0)
    expect(api.all('get', weatherPaths.secureList)).toHaveLength(0)
  })

  test('is a normal state: no error in the console and no toast', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()))

    const weather = weatherForecastPage(page)
    await weather.open()

    await expect(weather.at.login).toBeVisible()
    expect(weather.consoleErrors).toEqual([])
    expect(await weather.at.toasts.count()).toBe(0)
  })

  test('still sees an error when the BFF fails to read the session', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()), bffApi.userFails(aProblem().saying('The session store is down.')))

    const weather = weatherForecastPage(page)
    await weather.open()

    await expect(weather.at.login).toBeVisible()
    expect(await weather.at.toast('The session store is down.').count()).toBe(1)
  })

  test('goes to the BFF login with the page to come back to', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()), bffApi.loginOpens())
    const weather = weatherForecastPage(page)
    await weather.open()

    await weather.logIn()

    expect(api.last('get', bffPaths.login)?.query.get('returnUrl')).toBe('/')
  })
})

test.describe('a signed-in user', () => {
  test.use({ actor: as.user('Bob Smith') })

  test('sees a welcome with their name', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()))

    const weather = weatherForecastPage(page)
    await weather.open()

    await expect(weather.at.welcome('Bob Smith')).toBeVisible()
    await expect(weather.at.login).toHaveCount(0)
  })

  test('sees the secure forecast with the name of the caller', async ({ page, api }) => {
    api.mock(
      weatherApi.listReturns(aForecast().on('2026-03-14')),
      weatherApi.secureListReturns(aForecast().on('2026-03-14').requestedBy('Bob Smith')),
    )
    const weather = weatherForecastPage(page)
    await weather.open()

    await weather.showSecureForecast()

    await expect(weather.at.authenticatedNotice).toBeVisible()
    await expect(weather.at.day('Sat, Mar 14')).toContainText('Bob Smith')
    expect(api.last('get', weatherPaths.secureList)?.headers.get('x-csrf')).toBe('1')
  })

  test('logs out through the logout URL of the session', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()), bffApi.logoutOpens())
    const weather = weatherForecastPage(page)
    await weather.open()

    await weather.logOut()

    expect(api.last('get', bffPaths.logout)?.query.get('sid')).toBe('test-session')
  })

  test.describe('on a 320 px screen', () => {
    test.use({ actor: as.user('Admin User'), viewport: { width: 320, height: 800 } })

    test('sees the title and the welcome on one line each, with the user area under the title', async ({ page, api }) => {
      api.mock(weatherApi.listReturns(aForecast()))

      const weather = weatherForecastPage(page)
      await weather.open()

      expect(await weather.linesOf(weather.at.heading)).toBe(1)
      expect(await weather.linesOf(weather.at.welcome('Admin User'))).toBe(1)
      const title = await weather.boxOf(weather.at.heading)
      const logout = await weather.boxOf(weather.at.logout)
      expect(logout.y).toBeGreaterThanOrEqual(title.y + title.height)
    })
  })
})

test.describe('the loading placeholders', () => {
  const slowList = { ...weatherApi.listReturns(aForecast()), delayMs: 2_000 }

  test('hold still for a user who asks for reduced motion', async ({ page, api }) => {
    api.mock(slowList)
    const weather = weatherForecastPage(page)

    await weather.openWhileLoading()

    expect(await weather.runningAnimationsIn(weather.at.loading)).toBe(0)
  })

  test.describe('without a reduced motion preference', () => {
    test.use({ reducedMotion: 'no-preference' })

    test('pulse', async ({ page, api }) => {
      api.mock(slowList)
      const weather = weatherForecastPage(page)

      await weather.openWhileLoading()

      expect(await weather.runningAnimationsIn(weather.at.loading)).toBeGreaterThan(0)
    })
  })
})

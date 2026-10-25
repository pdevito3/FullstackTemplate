import { weatherApi } from '@tests/support/api/weather-api'
import { aForecast } from '@tests/support/builders/forecast-builder'
import { expect, test } from '@tests/support/fixtures'
import { weatherForecastPage } from '../weather/weather-forecast.tasks'
import { appShellPage } from './app-shell.tasks'

for (const viewport of [
  { width: 1280, height: 800 },
  { width: 390, height: 844 },
]) {
  test.describe(`a short page at ${viewport.width}x${viewport.height}`, () => {
    test.use({ viewport })

    test('does not scroll', async ({ page }) => {
      const shell = appShellPage(page)

      await shell.openAbout()

      expect(await shell.pageScrollRange()).toBe(0)
    })
  })
}

test.describe('the home page on a desktop', () => {
  test.use({ viewport: { width: 1280, height: 800 } })

  test('does not scroll when the forecast fits', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast().on('2026-03-14'), aForecast().on('2026-03-15'), aForecast().on('2026-03-16')))

    await weatherForecastPage(page).open()

    expect(await appShellPage(page).pageScrollRange()).toBe(0)
  })
})

import { weatherApi } from '@tests/support/api/weather-api'
import { aForecast } from '@tests/support/builders/forecast-builder'
import { expect, test } from '@tests/support/fixtures'
import { useDefaultFontSize } from '@tests/support/screen'
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

test.describe('the landmarks of a page', () => {
  test('the home page has one main landmark', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()))

    await weatherForecastPage(page).open()

    expect(await appShellPage(page).at.mainLandmarks.count()).toBe(1)
  })

  test('the about page has one main landmark and a level 1 heading', async ({ page }) => {
    const shell = appShellPage(page)

    await shell.openAbout()

    expect(await shell.at.mainLandmarks.count()).toBe(1)
    await expect(shell.at.aboutHeading).toBeVisible()
  })
})

test.describe('the layout header', () => {
  test.use({ viewport: { width: 1280, height: 720 } })

  test('centers the separator on the sidebar trigger', async ({ page }) => {
    const shell = appShellPage(page)

    await shell.openAbout()

    expect(await shell.verticalCenter(shell.at.headerSeparator)).toBeCloseTo(await shell.verticalCenter(shell.at.sidebarTrigger), 0)
  })
})

test.describe('the landmarks of the layout', () => {
  test('the router devtools add no contentinfo landmark', async ({ page }) => {
    const shell = appShellPage(page)

    await shell.openAbout()

    await expect(shell.at.routerDevtoolsButton).toBeVisible()
    expect(await shell.at.contentInfoLandmarks.count()).toBe(0)
  })

  test('the sidebar links are in a main navigation landmark', async ({ page }) => {
    const shell = appShellPage(page)

    await shell.openAbout()

    await expect(shell.at.mainNavigation.getByRole('link', { name: 'Home' })).toBeVisible()
    await expect(shell.at.mainNavigation.getByRole('link', { name: 'About' })).toBeVisible()
  })
})

test.describe('wide content next to the open sidebar', () => {
  test.use({ viewport: { width: 1024, height: 720 } })

  test('scrolls in its own box, and the page does not scroll sideways', async ({ page }) => {
    const shell = appShellPage(page)
    await shell.openAbout()

    await shell.addWideContent(1600)

    expect(await shell.sideScrollRange(shell.at.wideContent)).toBeGreaterThan(0)
    expect(await shell.pageSideScrollRange()).toBe(0)
  })
})

test.describe('an unknown address', () => {
  test('shows a page not found message in the app layout', async ({ page }) => {
    const shell = appShellPage(page)

    await shell.openMissingPage()

    await expect(shell.at.notFoundHeading).toBeVisible()
    await expect(shell.at.currentPage).toHaveText('Not Found')
    expect(shell.warnings.filter((warning) => warning.includes('notFound'))).toEqual([])
  })

  test('links to the home page', async ({ page, api }) => {
    api.mock(weatherApi.listReturns(aForecast()))
    const shell = appShellPage(page)
    await shell.openMissingPage()

    await shell.goHome()

    await expect(shell.at.weatherHeading).toBeVisible()
    await expect(shell.at.currentPage).toHaveText('Home')
  })
})

test.describe('the mobile sidebar with a 24 px default font', () => {
  test.use({ viewport: { width: 390, height: 844 } })

  test('fits in a 390 px window', async ({ page }) => {
    const shell = appShellPage(page)
    await useDefaultFontSize(page, 24)
    await shell.openAbout()

    await shell.openMobileSidebar()

    const sheet = await shell.at.mobileSidebar.boundingBox()
    expect(sheet!.x + sheet!.width).toBeLessThanOrEqual(390)
  })
})

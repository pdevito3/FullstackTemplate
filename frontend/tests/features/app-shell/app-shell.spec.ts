import { expect, test } from '@tests/support/fixtures'
import { useDefaultFontSize } from '@tests/support/screen'
import { homePage } from '../home/home.tasks'
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

    test('the home page does not scroll', async ({ page }) => {
      await homePage(page).open()

      expect(await appShellPage(page).pageScrollRange()).toBe(0)
    })
  })
}

test.describe('the landmarks of a page', () => {
  test('the home page has one main landmark', async ({ page }) => {
    await homePage(page).open()

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

    await expect(shell.at.homeNavigationLink).toBeVisible()
    await expect(shell.at.aboutNavigationLink).toBeVisible()
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

  test('links to the home page', async ({ page }) => {
    const shell = appShellPage(page)
    await shell.openMissingPage()

    await shell.goHome()

    await expect(homePage(page).at.heading).toBeVisible()
    await expect(shell.at.currentPage).toHaveText('Home')
  })
})

test.describe('the focus after a navigation', () => {
  test('a link in the page moves the focus to the level 1 heading of the new page', async ({ page }) => {
    const shell = appShellPage(page)
    await shell.openMissingPage()

    await shell.pressHomeLink()

    await expect(homePage(page).at.heading).toBeFocused()
  })

  test('a sidebar link keeps the focus on the link and announces the new page', async ({ page }) => {
    const shell = appShellPage(page)
    await homePage(page).open()

    await shell.pressAboutNavigationLink()

    await expect(shell.at.aboutNavigationLink).toBeFocused()
    await expect(shell.at.pageAnnouncement).toHaveText('About')
    await expect(shell.at.pageAnnouncement).toHaveAttribute('role', 'status')
  })

  test('a click on the content after the focus moves to the heading leaves the focus on the body', async ({ page }) => {
    const shell = appShellPage(page)
    await shell.openMissingPage()
    await shell.pressHomeLink()
    await expect(homePage(page).at.heading).toBeFocused()

    await shell.clickBlankContent()

    expect(await shell.focusedElement()).toBe('body')
  })
})

test.describe('the focus after a navigation on a phone', () => {
  test.use({ viewport: { width: 390, height: 844 } })

  test('a link of the mobile sidebar gives the focus back to the sidebar trigger and announces the new page', async ({ page }) => {
    const shell = appShellPage(page)
    await homePage(page).open()
    await shell.openMobileSidebar()

    await shell.openAboutFromMobileSidebar()

    await expect(shell.at.sidebarTrigger).toBeFocused()
    await expect(shell.at.pageAnnouncement).toHaveText('About')
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

test.describe('the mobile sidebar on a 320 px phone with a 24 px default font', () => {
  test.use({ viewport: { width: 320, height: 640 } })

  test('shows the whole app name', async ({ page }) => {
    const shell = appShellPage(page)
    await useDefaultFontSize(page, 24)
    await shell.openAbout()

    await shell.openMobileSidebar()

    const name = shell.at.mobileSidebarAppName
    await expect(name).toBeVisible()
    expect(await name.evaluate((el) => el.scrollWidth <= el.clientWidth && el.scrollHeight <= el.clientHeight)).toBe(true)
  })
})

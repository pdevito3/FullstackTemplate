import { expect, test } from '@tests/support/fixtures'
import { themePage } from './theme.tasks'

test.describe('the scrollbars and the native controls', () => {
  test('are dark in the dark theme when the OS is light', async ({ page }) => {
    const theme = themePage(page)
    await theme.useOsScheme('light')
    await theme.open()

    await theme.choose('Dark mode')

    await expect.poll(() => theme.colorScheme()).toBe('dark')
  })

  test('are light in the light theme when the OS is dark', async ({ page }) => {
    const theme = themePage(page)
    await theme.useOsScheme('dark')
    await theme.open()

    await theme.choose('Light mode')

    await expect.poll(() => theme.colorScheme()).toBe('light')
  })

  test('follow the OS in the system theme', async ({ page }) => {
    const theme = themePage(page)
    await theme.useOsScheme('light')
    await theme.open()
    await theme.choose('System theme')

    await theme.useOsScheme('dark')

    await expect.poll(() => theme.colorScheme()).toBe('dark')
  })
})

test.describe('the theme of the first paint', () => {
  const cases = [
    { choice: 'Dark mode', os: 'light', themeClass: 'dark' },
    { choice: 'Light mode', os: 'dark', themeClass: 'light' },
    { choice: 'System theme', os: 'dark', themeClass: 'dark' },
  ] as const

  for (const { choice, os, themeClass } of cases) {
    test(`is ${themeClass} before the app runs after the user chose ${choice} on a ${os} OS`, async ({ page }) => {
      const theme = themePage(page)
      await theme.useOsScheme(os)
      await theme.open()
      await theme.choose(choice)

      await theme.reloadWithoutTheApp()

      expect(await theme.themeClass()).toBe(themeClass)
    })
  }

  test('is dark before the app runs on the first visit on a dark OS', async ({ page }) => {
    const theme = themePage(page)
    await theme.useOsScheme('dark')
    await theme.open()

    await theme.reloadWithoutTheApp()

    expect(await theme.themeClass()).toBe('dark')
  })

  test('keeps the class of the OS when the stored theme is not a known value', async ({ page }) => {
    const theme = themePage(page)
    await theme.useOsScheme('dark')
    await theme.open()
    await theme.storeTheme('blue')

    await page.reload()

    await expect(theme.at.heading).toBeVisible()
    expect(await theme.themeClass()).toBe('dark')
  })
})

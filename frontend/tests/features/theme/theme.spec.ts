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

// Feature: navigation-and-theme. Sidebar links, breadcrumb, collapse, theme. See features/navigation-and-theme.md.
export default async ({ page, step, capture, settle, expect }) => {
  const breadcrumb = page.getByRole('navigation', { name: 'breadcrumb', exact: true })
  // The layout has a Toggle Sidebar button in the header and a rail with the same name. Use the header one.
  const toggleSidebar = page.getByRole('main').getByRole('button', { name: 'Toggle Sidebar' }).first()
  const announcement = page.getByTestId('page-announcement')

  await step('a link in the page: Go to the home page on a missing page gives the focus to the h1 of Home', async () => {
    await page.goto('/no-such-page')
    await page.getByRole('main').getByRole('link', { name: 'Go to the home page' }).click()
    await expect(page).toHaveURL(/\/$/)
    await expect(page.getByRole('heading', { level: 1 })).toBeFocused()
  })

  await step('open / : breadcrumb is Home', async () => {
    await page.goto('/')
    await expect(breadcrumb).toHaveText('Home')
  })

  await step('open / : only the Home item of the sidebar is active', async () => {
    const active = page.locator('[data-slot="sidebar"] [data-active]')
    await expect(active).toHaveCount(1)
    await expect(active).toHaveAccessibleName('Home')
    // A false flag must not render as an attribute, because the data-active style matches any value.
    await expect(page.locator('[data-active="false"]')).toHaveCount(0)
  })

  await step('choose About: URL /about, breadcrumb About, the link keeps the focus, and the status region says About', async () => {
    const about = page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'About' })
    await about.click()
    await expect(page).toHaveURL(/\/about$/)
    await expect(breadcrumb).toHaveText('About')
    await expect(page.getByRole('heading', { name: 'About', level: 1 })).toBeVisible()
    await expect(about).toBeFocused()
    await expect(announcement).toHaveText('About')
  })
  await capture('about')

  // example:start
  await step('open Examples, choose Filter Builder', async () => {
    await page.getByRole('button', { name: 'Examples' }).click()
    await page.getByRole('link', { name: 'Filter Builder' }).click()
    await expect(page).toHaveURL(/\/filter-demo$/)
    await expect(breadcrumb).toHaveText('Filter Builder')
    await expect(page.getByRole('heading', { name: 'Filter Builder Demo', level: 1 })).toBeVisible()
  })

  await step('choose Components', async () => {
    await page.getByRole('link', { name: 'Components' }).click()
    await expect(page).toHaveURL(/\/components$/)
    await expect(breadcrumb).toHaveText('Components')
    const active = page.locator('[data-slot="sidebar"] [data-active]')
    await expect(active).toHaveCount(2)
    await expect(active.nth(0)).toHaveAccessibleName('Examples')
    await expect(active.nth(1)).toHaveAccessibleName('Components')
  })
  await capture('components')
  // example:end

  await step('collapse the sidebar: the link labels hide', async () => {
    await toggleSidebar.click()
    await expect(page.locator('[data-slot="sidebar"][data-state="collapsed"]')).toHaveCount(1)
  })
  await capture('collapsed')
  await step('expand the sidebar', async () => {
    await toggleSidebar.click()
    await expect(page.locator('[data-slot="sidebar"][data-state="expanded"]')).toHaveCount(1)
  })

  await step('choose Dark mode: <html> gets class dark and keeps it after a reload', async () => {
    await page.getByRole('button', { name: 'Dark mode' }).click()
    await expect(page.locator('html')).toHaveClass(/\bdark\b/)
    expect(await page.evaluate(() => localStorage.getItem('ui-theme'))).toBe('dark')
    await page.reload()
    await expect(page.locator('html')).toHaveClass(/\bdark\b/)
  })
  await capture('dark')

  await step('choose Light mode: <html> gets class light', async () => {
    await page.getByRole('button', { name: 'Light mode' }).click()
    await expect(page.locator('html')).toHaveClass(/\blight\b/)
    await expect(page.locator('html')).not.toHaveClass(/\bdark\b/)
  })

  await step('choose System theme: the stored value is system', async () => {
    await page.getByRole('button', { name: 'System theme' }).click()
    expect(await page.evaluate(() => localStorage.getItem('ui-theme'))).toBe('system')
  })

  // The last step. No later step needs the desktop viewport.
  const mobileSidebar = page.locator('[data-slot="sidebar"][data-mobile="true"]')
  await step('mobile: the header toggle opens the sidebar sheet, 18rem wide, with no close button', async () => {
    await page.setViewportSize({ width: 390, height: 844 })
    await expect(mobileSidebar).toHaveCount(0)
    await toggleSidebar.click()
    await expect(mobileSidebar).toBeVisible()
    await settle()
    const box = await mobileSidebar.boundingBox()
    expect(Math.abs(box.width - 288)).toBeLessThanOrEqual(1)
    await expect(mobileSidebar.getByRole('button', { name: 'Close' })).toHaveCount(0)
  })
  await capture('mobile-sidebar', { fullPage: false })
  await step('mobile: choose About in the sheet: URL /about, the sheet closes, the focus is on the toggle, and the status region says About', async () => {
    await mobileSidebar.getByRole('link', { name: 'About' }).click()
    await expect(page).toHaveURL(/\/about$/)
    await expect(breadcrumb).toHaveText('About')
    await expect(mobileSidebar).toHaveCount(0)
    await expect(toggleSidebar).toBeFocused()
    await expect(announcement).toHaveText('About')
  })
  await capture('mobile-about', { fullPage: false })
}

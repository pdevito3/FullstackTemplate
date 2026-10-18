// Feature: navigation-and-theme. Sidebar links, Demos submenu, breadcrumb, collapse, theme. See features/navigation-and-theme.md.
export default async ({ page, step, capture, expect }) => {
  const breadcrumb = page.getByRole('navigation', { name: 'breadcrumb' })
  // The layout has a Toggle Sidebar button in the header and a rail with the same name. Use the header one.
  const toggleSidebar = page.getByRole('main').getByRole('button', { name: 'Toggle Sidebar' }).first()

  await step('open / : breadcrumb is Home', async () => {
    await page.goto('/')
    await expect(breadcrumb).toHaveText('Home')
  })

  await step('choose About: URL /about, breadcrumb About', async () => {
    await page.getByRole('link', { name: 'About' }).click()
    await expect(page).toHaveURL(/\/about$/)
    await expect(breadcrumb).toHaveText('About')
  })

  await step('open Demos, choose Filter Builder', async () => {
    await page.getByRole('button', { name: 'Demos' }).click()
    await page.getByRole('link', { name: 'Filter Builder' }).click()
    await expect(page).toHaveURL(/\/filter-demo$/)
    await expect(breadcrumb).toHaveText('Filter Builder')
    await expect(page.getByRole('heading', { name: 'Filter Builder Demo', level: 1 })).toBeVisible()
  })

  await step('choose Components', async () => {
    await page.getByRole('link', { name: 'Components' }).click()
    await expect(page).toHaveURL(/\/components$/)
    await expect(breadcrumb).toHaveText('Components')
  })
  await capture('components')

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
}

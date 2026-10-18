// Feature: filter-builder. The demo at /filter-demo turns filters into a QueryKit string. See features/filter-builder.md.
export default async ({ page, step, capture, expect }) => {
  // The card titled QueryKit String shows the generated string in a monospace block.
  const queryKitCard = page.locator('[data-slot="card"]').filter({ hasText: 'QueryKit String' })
  const queryKit = queryKitCard.locator('.font-mono')
  // A filter badge has its property, operator, and value in separate spans, so match with a pattern.
  const paulBadge = page.locator('[data-slot="badge"]').filter({ hasText: /First Name\s*\|\s*Contains\s*\|\s*Paul/ })

  await step('open /filter-demo: no filters', async () => {
    await page.goto('/filter-demo')
    await expect(queryKitCard).toContainText('No filters applied yet')
  })

  await step('add a text filter: First Name contains Paul', async () => {
    await page.getByRole('button', { name: 'Add Filter' }).click()
    const menu = page.getByRole('dialog')
    await menu.getByRole('textbox', { name: 'Search properties...' }).fill('first')
    await menu.getByRole('button', { name: 'First Name' }).click()
    const dialog = page.getByRole('dialog', { name: 'Add First Name Filter' })
    await expect(dialog.getByRole('button', { name: 'Add Filter' })).toBeDisabled()
    await dialog.getByRole('textbox', { name: 'Value' }).fill('Paul')
    await dialog.getByRole('button', { name: 'Add Filter' }).click()
    await expect(dialog).toHaveCount(0)
    await expect(paulBadge).toBeVisible()
    await expect(queryKit).toHaveText('firstName @=* Paul')
  })
  await capture('text-filter')

  await step('choose the Urgent Bugs preset: it replaces the filters', async () => {
    await page.getByRole('button', { name: 'Urgent Bugs' }).click()
    await expect(paulBadge).toHaveCount(0)
    await expect(queryKit).toHaveText('labels ^$ [bug] && labels ^$ [urgent]')
  })

  await step('choose Switch to OR: the operator becomes ||', async () => {
    await page.getByRole('button', { name: 'Switch to OR' }).click()
    await expect(queryKit).toHaveText('labels ^$ [bug] || labels ^$ [urgent]')
    await expect(page.getByRole('button', { name: 'Switch to AND' })).toBeVisible()
  })
  await capture('preset-or')

  await step('choose Clear: no filters', async () => {
    await page.getByRole('button', { name: 'Clear' }).click()
    await expect(queryKitCard).toContainText('No filters applied yet')
    await expect(queryKit).toHaveCount(0)
  })
}

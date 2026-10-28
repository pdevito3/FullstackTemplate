// Feature: filter-builder. The demo at /filter-demo turns filters into a QueryKit string. See features/examples/filter-builder.md.
export default async ({ page, step, capture, settle, expect }) => {
  // The card titled QueryKit String shows the generated string in a monospace block.
  const queryKitCard = page.locator('[data-slot="card"]').filter({ hasText: 'QueryKit String' })
  const queryKit = queryKitCard.locator('.font-mono')
  // A filter badge has its property, operator, and value in separate spans, so match with a pattern.
  const badge = (pattern) => page.locator('[data-slot="badge"]').filter({ hasText: pattern })
  const paulBadge = badge(/First Name\s*\|\s*Contains\s*\|\s*Paul/)
  const statusBadge = badge(/^Status/)
  const propertyMenu = page.getByRole('menu', { name: 'Add Filter' })

  // Record the Examples panel of the sidebar in each of the first 20 frames after it mounts.
  // Motion animates the height in JavaScript, so getAnimations() does not see it. Read the computed style.
  await page.addInitScript(() => {
    if (location.pathname !== '/filter-demo') return
    const samples = []
    window.__demosPanelSamples = samples
    const tick = () => {
      const panel = document.querySelector('[data-slot="collapsible-content"]')
      if (panel) {
        samples.push({
          opacity: Number(getComputedStyle(panel).opacity),
          height: panel.getBoundingClientRect().height,
          fullHeight: panel.scrollHeight,
        })
      }
      if (samples.length < 20) requestAnimationFrame(tick)
    }
    requestAnimationFrame(tick)
  })

  await step('open /filter-demo: no filters', async () => {
    await page.goto('/filter-demo')
    await expect(queryKitCard).toContainText('No filters applied yet')
  })

  await step('page load: the expanded Examples panel shows without a mount animation', async () => {
    await expect.poll(() => page.evaluate(() => window.__demosPanelSamples.length)).toBe(20)
    const samples = await page.evaluate(() => window.__demosPanelSamples)
    for (const sample of samples) {
      expect(sample.opacity).toBe(1)
      expect(sample.fullHeight).toBeGreaterThan(0)
      expect(Math.abs(sample.height - sample.fullHeight)).toBeLessThanOrEqual(1)
    }
  })

  await step('add a text filter: First Name contains Paul', async () => {
    await page.getByRole('button', { name: 'Add Filter' }).click()
    // The search also matches the property key: "address" is only in the key author.address.city.
    await page.getByRole('searchbox', { name: 'Search properties' }).fill('address')
    await expect(propertyMenu.getByRole('menuitem')).toHaveCount(1)
    await expect(propertyMenu.getByRole('menuitem', { name: 'Author City' })).toBeVisible()
    await page.getByRole('searchbox', { name: 'Search properties' }).fill('first')
    await expect(propertyMenu.getByRole('menuitem')).toHaveCount(1)
    await propertyMenu.getByRole('menuitem', { name: 'First Name' }).click()
    const dialog = page.getByRole('dialog', { name: 'Add First Name Filter' })
    await expect(propertyMenu).toHaveCount(0)
    await expect(dialog.getByRole('button', { name: 'Add Filter' })).toBeDisabled()
    await expect(dialog.getByRole('textbox', { name: 'Value' })).toBeFocused()
    await dialog.getByRole('textbox', { name: 'Value' }).fill('Paul')
    await dialog.getByRole('button', { name: 'Add Filter' }).click()
    await expect(dialog).toHaveCount(0)
    await expect(paulBadge).toBeVisible()
    await expect(queryKit).toHaveText('firstName @=* Paul')
  })
  await capture('text-filter')

  await step('add a submenu filter: Status is Open', async () => {
    await page.getByRole('button', { name: 'Add Filter' }).click()
    await propertyMenu.getByRole('menuitem', { name: 'Status' }).click()
    const statusFilter = page.getByRole('dialog', { name: 'Status filter' })
    await expect(statusFilter).toBeVisible()
    await capture('status-submenu', { fullPage: false })
    // React Aria hides the native input, so click the label as a user does.
    await statusFilter.getByText('Open', { exact: true }).click()
    await expect(statusFilter.getByRole('checkbox', { name: 'Open' })).toBeChecked()
    await statusFilter.getByRole('button', { name: 'Add (1)' }).click()
    await expect(statusFilter).toHaveCount(0)
    await expect(propertyMenu).toHaveCount(0)
    await expect(statusBadge).toBeVisible()
    await expect(queryKit).toContainText('firstName @=* Paul && status ^^ [open]')
  })

  await step('edit the First Name badge: the value becomes Pat', async () => {
    await paulBadge.getByRole('button', { name: 'First Name | Contains | Paul', exact: true }).click()
    const dialog = page.getByRole('dialog', { name: 'Edit First Name Filter' })
    await dialog.getByRole('textbox', { name: 'Value' }).fill('Pat')
    await dialog.getByRole('button', { name: 'Update Filter' }).click()
    await expect(dialog).toHaveCount(0)
    await expect(queryKit).toHaveText('firstName @=* Pat && status ^^ [open]')
  })

  await step('remove the Status badge with its named x button', async () => {
    await page.getByRole('button', { name: /^Remove filter: Status \|/ }).click()
    await expect(statusBadge).toHaveCount(0)
    await expect(queryKit).toHaveText('firstName @=* Pat')
  })

  await step('choose the Urgent Bugs preset: it replaces the filters', async () => {
    await page.getByRole('button', { name: 'Urgent Bugs' }).click()
    await expect(badge(/First Name/)).toHaveCount(0)
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

  await step('edit the date badge of Recently Updated: the Date input has the focus', async () => {
    await page.getByRole('button', { name: 'Recently Updated' }).click()
    await page.getByRole('button', { name: /^Updated Date \| Equals \| After / }).click()
    const dialog = page.getByRole('dialog', { name: 'Edit Updated Date Filter' })
    await expect(dialog).toBeVisible()
    // The date control loads lazily: the dialog shows a placeholder until its chunk is in.
    // React Aria then focuses the first segment of the date field.
    await expect(dialog.getByRole('group', { name: 'Date' }).getByRole('spinbutton').first()).toBeFocused()
    await expect(dialog.getByRole('status', { name: 'Loading filter' })).toHaveCount(0)
    await capture('date-edit', { fullPage: false })
    await page.keyboard.press('Escape')
    await expect(dialog).toHaveCount(0)
    await page.getByRole('button', { name: 'Clear' }).click()
    await expect(queryKitCard).toContainText('No filters applied yet')
  })

  await step('group two filters: the QueryKit string gets parentheses', async () => {
    await page.getByRole('button', { name: 'Urgent Bugs' }).click()
    await expect(queryKit).toHaveText('labels ^$ [bug] && labels ^$ [urgent]')
    await page.getByRole('button', { name: 'Group Filters' }).click()
    await expect(page.getByRole('button', { name: 'Group Filters' })).toBeDisabled()
    // Each checkbox is named after the badge text. React Aria hides the native input,
    // so click its visible label as a user does.
    for (const value of ['Bug', 'Urgent']) {
      const checkbox = page.getByRole('checkbox', { name: `Select filter: Labels | Has | ${value}` })
      await checkbox.locator('xpath=ancestor::label').click()
      await expect(checkbox).toBeChecked()
    }
    await capture('grouping-selection')
    await page.getByRole('button', { name: 'Group Selected (2)' }).click()
    await expect(page.getByText('Group (Depth 1)')).toBeVisible()
    await expect(queryKit).toHaveText('(labels ^$ [bug] && labels ^$ [urgent])')
  })
  await capture('grouped')

  await step('choose Ungroup: the filters go back to the root without parentheses', async () => {
    await page.getByRole('button', { name: 'Ungroup' }).click()
    await expect(page.getByText('Group (Depth 1)')).toHaveCount(0)
    await expect(queryKit).toHaveText('labels ^$ [bug] && labels ^$ [urgent]')
    await page.getByRole('button', { name: 'Clear' }).click()
    await expect(queryKitCard).toContainText('No filters applied yet')
  })

  await step('add a multiselect filter with two values: Status is Open or In Progress', async () => {
    await page.getByRole('button', { name: 'Add Filter' }).click()
    await propertyMenu.getByRole('menuitem', { name: 'Status' }).click()
    const statusFilter = page.getByRole('dialog', { name: 'Status filter' })
    await expect(statusFilter).toBeVisible()
    await settle()
    await statusFilter.getByText('Open', { exact: true }).click()
    await statusFilter.getByText('In Progress', { exact: true }).click()
    await expect(statusFilter.getByRole('checkbox', { name: 'Open' })).toBeChecked()
    await expect(statusFilter.getByRole('checkbox', { name: 'In Progress' })).toBeChecked()
    await statusFilter.getByRole('button', { name: 'Add (2)' }).click()
    await expect(statusFilter).toHaveCount(0)
    await expect(badge(/Status\s*\|.*Open, In Progress/)).toBeVisible()
    await expect(queryKit).toHaveText('status ^^ [open, in-progress]')
  })
  await capture('multiselect-two-values')

  await step('remove the Status badge: no filters', async () => {
    await page.getByRole('button', { name: /^Remove filter: Status \|/ }).click()
    await expect(queryKitCard).toContainText('No filters applied yet')
  })

  await step('add a number filter: Comment Count equals 5', async () => {
    await page.getByRole('button', { name: 'Add Filter' }).click()
    await page.getByRole('searchbox', { name: 'Search properties' }).fill('comment')
    await propertyMenu.getByRole('menuitem', { name: 'Comment Count' }).click()
    const numberFilter = page.getByRole('dialog', { name: 'Comment Count filter' })
    await expect(numberFilter).toBeVisible()
    await capture('number-filter', { fullPage: false })
    await expect(numberFilter.getByRole('button', { name: /Count Equals/ })).toBeVisible()
    await numberFilter.getByRole('spinbutton', { name: 'Value' }).fill('5')
    await numberFilter.getByRole('button', { name: 'Add Filter' }).click()
    await expect(numberFilter).toHaveCount(0)
    await expect(badge(/Comment Count\s*\|\s*Count Equals\s*\|\s*5/)).toBeVisible()
    await expect(queryKit).toHaveText('comments #== 5')
  })

  // Without its own overlay state, the operator Select in a submenu throws the React Aria error "Duplicate key"
  // when the submenu closes before the Select ever opened. The router error boundary catches it, so it is no
  // page error: the page shows "Something went wrong!" instead of the demo.
  await step('number filter operator: Escape closes the submenu, then choose Count Greater Than in its Select', async () => {
    await page.getByRole('button', { name: /^Remove filter: Comment Count \|/ }).click()
    await expect(queryKitCard).toContainText('No filters applied yet')
    await page.getByRole('button', { name: 'Add Filter' }).click()
    await page.getByRole('searchbox', { name: 'Search properties' }).fill('comment')
    const commentCount = propertyMenu.getByRole('menuitem', { name: 'Comment Count' })
    const numberFilter = page.getByRole('dialog', { name: 'Comment Count filter' })
    await commentCount.click()
    await expect(numberFilter).toBeVisible()
    await settle()
    // Escape closes only the submenu. The property menu stays open.
    await page.keyboard.press('Escape')
    await expect(numberFilter).toHaveCount(0)
    await expect(page.getByRole('heading', { name: 'Filter Builder Demo' })).toBeVisible()
    await expect(propertyMenu).toBeVisible()

    await commentCount.click()
    await expect(numberFilter).toBeVisible()
    await settle()
    await numberFilter.getByRole('button', { name: 'Count Equals Operator' }).click()
    const operators = page.getByRole('listbox', { name: 'Operator' })
    await expect(operators.getByRole('option', { name: 'Count Equals' })).toHaveAttribute('aria-selected', 'true')
    await capture('number-operator-open', { fullPage: false })
    await settle()
    await operators.getByRole('option', { name: 'Count Greater Than', exact: true }).click()
    await expect(operators).toHaveCount(0)
    await expect(numberFilter.getByRole('button', { name: 'Count Greater Than Operator' })).toBeVisible()
    await numberFilter.getByRole('spinbutton', { name: 'Value' }).fill('5')
    await numberFilter.getByRole('button', { name: 'Add Filter' }).click()
    await expect(numberFilter).toHaveCount(0)
    await expect(badge(/Comment Count\s*\|\s*Count Greater Than\s*\|\s*5/)).toBeVisible()
    await expect(queryKit).toHaveText('comments #> 5')
  })
  await capture('number-operator')
}

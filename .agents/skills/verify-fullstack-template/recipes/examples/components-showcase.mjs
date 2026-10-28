// Feature: components-showcase. The /components page shows each shared UI component. See features/examples/components-showcase.md.
export default async ({ page, step, capture, settle, expect }) => {
  await step('open /components: all sections show', async () => {
    await page.goto('/components')
    await expect(page.getByRole('heading', { name: 'Component Library' })).toBeVisible()
    await expect(page.getByRole('grid', { name: 'Users' }).getByRole('row')).toHaveCount(4)
  })

  await step('page load: the inline command search box does not take the focus', async () => {
    await expect(page.getByRole('searchbox').first()).not.toBeFocused()
    expect(await page.evaluate(() => document.activeElement?.tagName)).toBe('BODY')
  })

  await step('sidebar: only the Examples parent and the Components item are active', async () => {
    const sidebar = page.locator('[data-slot="sidebar"]')
    const active = sidebar.locator('[data-active]')
    await expect(active).toHaveCount(2)
    await expect(active.nth(0)).toHaveAccessibleName('Examples')
    await expect(active.nth(1)).toHaveAccessibleName('Components')
    // A false flag must not render as an attribute, because the data-active and data-inset styles match any value.
    await expect(page.locator('[data-active="false"], [data-inset="false"]')).toHaveCount(0)
  })

  await step('avatar: the initials show until the image loads', async () => {
    // Hold the image request, so that the step can see the avatar while the image loads.
    let release
    const held = new Promise((resolve) => (release = resolve))
    const isAvatar = (url) => url.pathname === '/avatar.png'
    await page.route(isAvatar, async (route) => {
      await held
      await route.continue()
    })
    await page.reload()
    const avatar = page.locator('[data-slot="avatar"]').first()
    await expect(avatar.locator('[data-slot="avatar-fallback"]')).toBeVisible()
    await expect(avatar.locator('[data-slot="avatar-fallback"]')).toHaveText('CN')
    await expect(avatar.locator('[data-slot="avatar-image"]')).toBeHidden()
    release()
    await expect(avatar.locator('[data-slot="avatar-image"]')).toBeVisible()
    await expect(avatar.locator('[data-slot="avatar-fallback"]')).toBeHidden()
    await page.unroute(isAvatar)
  })

  await step('layout: the Select fills its column and the date picker is as tall as an Input', async () => {
    const trigger = page.getByRole('button', { name: /Select \(Dropdown\)/ })
    // The Select and the other pickers share one grid column. The trigger must be as wide as that column.
    const [triggerWidth, columnWidth] = await trigger.evaluate((el) => [
      el.getBoundingClientRect().width,
      el.closest('.grid').getBoundingClientRect().width,
    ])
    expect(Math.abs(triggerWidth - columnWidth)).toBeLessThanOrEqual(1)
    const dateBox = await page.getByRole('group', { name: 'Start date' }).boundingBox()
    const inputBox = await page.getByRole('textbox', { name: 'Email' }).boundingBox()
    expect(inputBox.height).toBe(32)
    expect(Math.abs(dateBox.height - inputBox.height)).toBeLessThanOrEqual(0.5)
  })
  await capture('page')

  await step('select: choose Svelte', async () => {
    await page.getByRole('button', { name: /Select \(Dropdown\)/ }).click()
    // Wait for the popover animation. A click on a moving option makes Playwright scroll, and React Aria closes a popover on scroll.
    await settle()
    await page.getByRole('option', { name: 'Svelte' }).click()
    await expect(page.getByText('Selected: Svelte')).toBeVisible()
  })

  await step('combobox: type vu and choose Vue', async () => {
    await page.getByRole('combobox', { name: 'Combobox (Searchable Single Select)' }).fill('vu')
    await expect(page.getByRole('option')).toHaveCount(1)
    await settle()
    await page.getByRole('option', { name: 'Vue' }).click()
    await expect(page.getByText('Selected: Vue')).toBeVisible()
  })

  await step('autocomplete: choose Germany, then type free text', async () => {
    const input = page.getByRole('combobox', { name: 'Autocomplete (Free-form with Suggestions)' })
    await input.fill('ger')
    await settle()
    await page.getByRole('option', { name: 'Germany' }).click()
    await expect(page.getByText('Value: Germany')).toBeVisible()
    await input.fill('Atlantis')
    await input.press('Escape')
    await expect(page.getByText('Value: Atlantis')).toBeVisible()
    await expect(page.getByRole('option')).toHaveCount(0)
    await input.locator('xpath=ancestor::*[@data-slot="input-group"]').getByRole('button', { name: 'Clear' }).click()
    await expect(input).toHaveValue('')
    await expect(input).toBeFocused()
    await input.press('Escape')
  })

  await step('multi-select: choose TypeScript and Rust as chips', async () => {
    const input = page.getByRole('combobox', { name: 'Select languages...' })
    await input.scrollIntoViewIfNeeded()
    await settle()
    await input.click()
    await settle()
    await page.getByRole('option', { name: 'TypeScript' }).click()
    await settle()
    await page.getByRole('option', { name: 'Rust' }).click()
    await page.keyboard.press('Escape')
    await expect(page.getByText('Selected: TypeScript, Rust')).toBeVisible()
    await expect(page.getByRole('grid', { name: 'Selected items' }).getByRole('row')).toHaveCount(2)
  })

  await step('multi-select: the chips and the input share one line', async () => {
    const input = page.getByRole('combobox', { name: 'Select languages...' })
    // The chips animate in. Measure after the animation ends.
    await settle()
    const field = await page.locator('[data-slot="combobox-chips"]').filter({ has: input }).boundingBox()
    expect(Math.abs(field.height - 32)).toBeLessThanOrEqual(1)
    const inputBox = await input.boundingBox()
    const inputCenter = inputBox.y + inputBox.height / 2
    for (const chip of await page.getByRole('grid', { name: 'Selected items' }).getByRole('row').all()) {
      const box = await chip.boundingBox()
      expect(Math.abs(box.y + box.height / 2 - inputCenter)).toBeLessThanOrEqual(2)
      expect(box.x + box.width).toBeLessThanOrEqual(inputBox.x + 1)
    }
  })
  await capture('form-controls')

  await step('dialog: open', async () => {
    await page.getByRole('button', { name: 'Open Dialog' }).click()
    await expect(page.getByRole('dialog', { name: 'Dialog Title' })).toBeVisible()
  })
  await capture('dialog', { fullPage: false })
  await step('dialog: Continue closes it', async () => {
    await page.getByRole('button', { name: 'Continue' }).click()
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  await step('sheet: open from the right', async () => {
    await page.getByRole('button', { name: 'Open Sheet' }).click()
    await expect(page.getByRole('dialog', { name: 'Sheet Title' })).toBeVisible()
  })
  await capture('sheet', { fullPage: false })
  await step('sheet: Escape closes it', async () => {
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  await step('drawer: open from the bottom', async () => {
    await page.getByRole('button', { name: 'Open Drawer' }).click()
    await expect(page.getByRole('dialog', { name: 'Drawer Title' })).toBeVisible()
  })
  await capture('drawer', { fullPage: false })
  await step('drawer: Done closes it', async () => {
    await page.getByRole('button', { name: 'Done' }).click()
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  await step('dropdown menu: Options shows three items', async () => {
    await page.getByRole('button', { name: 'Options' }).click()
    await expect(page.getByRole('menu', { name: 'Options' }).getByRole('menuitem')).toHaveCount(3)
    // No item in this menu is inset. A false inset flag must not render as an attribute.
    await expect(page.getByRole('menu', { name: 'Options' }).locator('[data-inset]')).toHaveCount(0)
  })
  await capture('dropdown', { fullPage: false })
  await step('dropdown menu: Profile closes it', async () => {
    await page.getByRole('menu', { name: 'Options' }).getByRole('menuitem', { name: 'Profile' }).click()
    await expect(page.getByRole('menu', { name: 'Options' })).toHaveCount(0)
  })

  await step('popover: Pick a date shows the calendar', async () => {
    await page.getByRole('button', { name: 'Pick a date' }).click()
    await expect(page.getByRole('dialog', { name: 'Pick a date' }).getByRole('grid')).toBeVisible()
  })
  await capture('popover', { fullPage: false })
  await step('popover: Escape closes it', async () => {
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  await step('popover: Select month and Select year move the focus into their picker and keep the width', async () => {
    const popover = page.getByRole('dialog', { name: 'Pick a date' })
    const hasFocus = (locator) => locator.evaluate((el) => el.contains(document.activeElement))
    await page.getByRole('button', { name: 'Pick a date' }).click()
    await expect(popover.getByRole('grid')).toBeVisible()
    await settle()
    const width = (await popover.boundingBox()).width
    await popover.getByRole('button', { name: 'Select month' }).click()
    const monthPicker = popover.getByRole('listbox', { name: 'Month picker' })
    await expect.poll(() => hasFocus(monthPicker)).toBe(true)
    await settle()
    expect(Math.abs((await popover.boundingBox()).width - width)).toBeLessThanOrEqual(1)
    // The picker is a grid of 3 columns: ArrowUp or ArrowDown moves the focus by one row.
    const focusedIndex = () =>
      monthPicker.evaluate((el) => [...el.querySelectorAll('[role="option"]')].indexOf(document.activeElement))
    const start = await focusedIndex()
    await page.keyboard.press(start >= 3 ? 'ArrowUp' : 'ArrowDown')
    await expect.poll(focusedIndex).toBe(start >= 3 ? start - 3 : start + 3)
    await popover.getByRole('button', { name: 'Select year' }).click()
    await expect.poll(() => hasFocus(popover.getByRole('listbox', { name: 'Year picker' }))).toBe(true)
    await settle()
    expect(Math.abs((await popover.boundingBox()).width - width)).toBeLessThanOrEqual(1)
  })
  await capture('popover-year', { fullPage: false })
  await step('popover: Escape in the year picker closes it', async () => {
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  await step('popover: pick a year, then a month: the focus is on a day cell, Escape closes it', async () => {
    const popover = page.getByRole('dialog', { name: 'Pick a date' })
    await page.getByRole('button', { name: 'Pick a date' }).click()
    await settle()
    await popover.getByRole('button', { name: 'Select month' }).click()
    await popover.getByRole('button', { name: 'Select year' }).click()
    await popover.getByRole('listbox', { name: 'Year picker' }).getByRole('option').nth(2).click()
    const monthPicker = popover.getByRole('listbox', { name: 'Month picker' })
    await expect.poll(() => monthPicker.evaluate((el) => el.contains(document.activeElement))).toBe(true)
    await monthPicker.getByRole('option').nth(2).click()
    await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('[role="gridcell"]'))).toBe(true)
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  await step('tooltip: hover Notifications', async () => {
    // React Aria opens a tooltip on hover only in pointer modality, and a pointermove sets it.
    // A real mouse moves over the page first. Playwright jumps onto the button, so move once before.
    await page.mouse.move(0, 0)
    await page.getByRole('button', { name: 'Notifications' }).hover()
    await expect(page.getByRole('tooltip')).toHaveText('You have 3 notifications')
  })
  await capture('tooltip', { fullPage: false })

  await step('command: type calc in the inline command', async () => {
    await page.getByRole('searchbox').first().fill('calc')
    await expect(page.getByRole('menuitem', { name: 'Calculator' })).toBeVisible()
    await expect(page.getByRole('menuitem', { name: 'Calendar' })).toHaveCount(0)
  })

  await step('command dialog: open, type sett, Enter closes it', async () => {
    await page.getByRole('button', { name: 'Open Command Menu' }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()
    await dialog.getByRole('searchbox').fill('sett')
    await expect(dialog.getByRole('menuitem')).toHaveCount(1)
    await capture('command-dialog', { fullPage: false })
    await page.keyboard.press('Enter')
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  await step('toast: Success shows a toast, x closes it', async () => {
    await page.getByRole('button', { name: 'Success', exact: true }).click()
    const toast = page.getByRole('alertdialog').or(page.getByRole('status')).filter({ hasText: 'Changes saved' })
    await expect(toast).toBeVisible()
    await capture('toast', { fullPage: false })
    await toast.getByRole('button', { name: 'Close' }).click()
    // The toast closes by itself after 5 s. A short timeout proves that Close removed it.
    await expect(page.getByText('Changes saved')).toHaveCount(0, { timeout: 1000 })
  })

  await step('collapsible: Toggle shows and hides the items', async () => {
    const toggle = page.getByRole('button', { name: 'Toggle', exact: true })
    await expect(page.getByText('@radix-ui/colors')).toBeHidden()
    await toggle.click()
    await expect(page.getByText('@radix-ui/colors')).toBeVisible()
    await toggle.click()
    await expect(page.getByText('@radix-ui/colors')).toBeHidden()
  })

  await step('responsive dialog on mobile: a bottom sheet', async () => {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.getByRole('button', { name: 'Open Dialog' }).click()
    await expect(page.getByRole('dialog', { name: 'Dialog Title' })).toBeVisible()
  })
  await capture('dialog-mobile', { fullPage: false })
}

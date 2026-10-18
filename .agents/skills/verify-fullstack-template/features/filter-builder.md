# Filter builder

The Filter Builder demo lets a user build a filter from typed properties and see the result as a QueryKit string. The user adds filters from a property menu, uses presets, changes the root operator between AND and OR, and clears all filters. The page shows the QueryKit string and the filter state as JSON. It sends no request to the API.

## Sub-features

- `filter-add-text` adds a text filter (for example First Name, Contains) through the property menu and the filter dialog.
- `filter-presets` replaces all filters with a preset: `My Open Issues`, `Urgent Bugs`, `Recently Updated`, or `Complex Nested Query`.
- `filter-logic` changes the root operator with `Switch to OR` and `Switch to AND`.
- `filter-clear` removes all filters.
- `filter-output` shows the QueryKit string in the `QueryKit String` card, and the state in the `Filter State (JSON)` card.

## How to get to it (user POV)

- Open `/filter-demo`.
- In the sidebar, choose `Demos`, then `Filter Builder`.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- A new page load has no filters.

The QueryKit output is `page.locator('[data-slot="card"]').filter({ hasText: 'QueryKit String' }).locator('.font-mono')`. Below, it is called `queryKit`. A filter badge is `page.locator('[data-slot="badge"]').filter({ hasText: /First Name\s*\|\s*Contains\s*\|\s*Paul/ })`.

- **Empty state.** Open `/filter-demo`. Run `page.goto('/filter-demo')`. The `QueryKit String` card contains `No filters applied yet`.
- **Open the property menu.** Choose `Add Filter`. Run `page.getByRole('button', { name: 'Add Filter' }).click()`. A dialog with the textbox `Search properties...` opens.
- **Pick a property.** Type `first` and choose `First Name`. Run `fill('first')` on the textbox, then `page.getByRole('dialog').getByRole('button', { name: 'First Name' }).click()`. The dialog `Add First Name Filter` opens. Its `Add Filter` button is disabled.
- **Add the filter.** Type the value and submit. Run `dialog.getByRole('textbox', { name: 'Value' }).fill('Paul')`, then `dialog.getByRole('button', { name: 'Add Filter' }).click()`. The dialog closes. The badge `First Name | Contains | Paul` is visible. `queryKit` is `firstName @=* Paul`.
- **Preset.** Choose `Urgent Bugs`. Run `page.getByRole('button', { name: 'Urgent Bugs' }).click()`. The First Name badge goes away. `queryKit` is `labels ^$ [bug] && labels ^$ [urgent]`.
- **OR.** Choose `Switch to OR`. Run `page.getByRole('button', { name: 'Switch to OR' }).click()`. `queryKit` is `labels ^$ [bug] || labels ^$ [urgent]`. The button name changes to `Switch to AND`.
- **Clear.** Choose `Clear`. Run `page.getByRole('button', { name: 'Clear' }).click()`. The card contains `No filters applied yet` again.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/filter-builder.mjs`. The captures `text-filter` and `preset-or` show the badges and the QueryKit string.

## Gotchas

- Two buttons are named `Add Filter`: the toolbar button and the submit button of the filter dialog. Scope the second one to `getByRole('dialog', { name: 'Add First Name Filter' })`.
- The property menu is a `dialog` without a name. Its items are buttons, not menu items.
- Text filters are case-insensitive by default. The operator gets a `*` suffix (`@=*`). The `Case sensitive` checkbox removes it.
- A preset replaces the filters. It does not add to them.
- `Switch to OR` shows only when there are two or more filters.
- The ARIA snapshot shows a badge as `First Name | Contains | Paul`, but the DOM has no spaces between its parts. `getByText('First Name | Contains | Paul')` finds nothing. Match the badge with a pattern.
- The x button on a filter badge has no accessible name. To remove one filter, use `<badge locator>.getByRole('button')`. This is a known accessibility defect.
- The filter state is local React state. A reload clears it.

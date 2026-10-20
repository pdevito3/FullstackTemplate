# Filter builder

The Filter Builder demo lets a user build a filter from typed properties and see the result as a QueryKit string. The user adds filters from a property menu, edits and removes badges, uses presets, changes the root operator between AND and OR, and clears all filters. The page shows the QueryKit string and the filter state as JSON. It sends no request to the API.

## Sub-features

- `filter-add-text` adds a text filter (for example First Name, Contains) through the property menu and the filter dialog.
- `filter-add-multiselect` adds a multiselect filter (for example Status) through a submenu of the property menu, with one value or more.
- `filter-add-number` adds a number filter (for example Comment Count) through a submenu of the property menu.
- `filter-number-operator` changes the operator of a number filter with the `Operator` Select in its submenu.
- `filter-group` puts the selected filters in a group, and `Ungroup` moves them back to the parent.
- `filter-search` filters the property menu by the label or by the property key, case-insensitive. For example, `address` finds `Author City` (key `author.address.city`).
- `filter-edit` changes the value of a filter through its badge. The edit dialog focuses the first input of the control.
- `filter-remove` removes one filter with the x button of its badge.
- `filter-presets` replaces all filters with a preset: `My Open Issues`, `Urgent Bugs`, `Recently Updated`, or `Complex Nested Query`.
- `filter-logic` changes the root operator with `Switch to OR` and `Switch to AND`.
- `filter-clear` removes all filters.
- `filter-output` shows the QueryKit string in the `QueryKit String` card, and the state in the `Filter State (JSON)` card.
- `filter-sidebar-mount` shows the open `Demos` panel of the sidebar on page load without an animation.

## How to get to it (user POV)

- Open `/filter-demo`.
- In the sidebar, choose `Demos`, then `Filter Builder`.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- A new page load has no filters.

The QueryKit output is `page.locator('[data-slot="card"]').filter({ hasText: 'QueryKit String' }).locator('.font-mono')`. Below, it is called `queryKit`. A filter badge is `page.locator('[data-slot="badge"]').filter({ hasText: /First Name\s*\|\s*Contains\s*\|\s*Paul/ })`. The property menu is `page.getByRole('menu', { name: 'Add Filter' })`.

- **Empty state.** Open `/filter-demo`. Run `page.goto('/filter-demo')`. The `QueryKit String` card contains `No filters applied yet`.
- **No mount animation.** Before the load, `page.addInitScript` records the `[data-slot="collapsible-content"]` panel in each of the first 20 frames after it mounts. In each frame, the computed `opacity` is 1 and the height is equal to `scrollHeight`.
- **Open the property menu.** Choose `Add Filter`. Run `page.getByRole('button', { name: 'Add Filter' }).click()`. The menu `Add Filter` opens, with the searchbox `Search properties`.
- **Search by key.** Type `address`. Run `page.getByRole('searchbox', { name: 'Search properties' }).fill('address')`. The menu shows only one item, `Author City`. The label does not contain `address`, but the key `author.address.city` does.
- **Pick a property.** Type `first` and choose `First Name`. Run `page.getByRole('searchbox', { name: 'Search properties' }).fill('first')`, then `propertyMenu.getByRole('menuitem', { name: 'First Name' }).click()`. The menu closes. The dialog `Add First Name Filter` opens. Its `Add Filter` button is disabled. The textbox `Value` has the focus.
- **Add the filter.** Type the value and submit. Run `dialog.getByRole('textbox', { name: 'Value' }).fill('Paul')`, then `dialog.getByRole('button', { name: 'Add Filter' }).click()`. The dialog closes. The badge `First Name | Contains | Paul` is visible. `queryKit` is `firstName @=* Paul`.
- **Submenu filter.** Open the property menu and choose `Status`. Run `propertyMenu.getByRole('menuitem', { name: 'Status' }).click()`. The dialog `Status filter` opens next to the menu. After it is visible, capture it. Choose `Open`, then `Add (1)`. Both overlays close. `queryKit` contains `status ^^ [open]`.
- **Edit.** Choose the badge text. Run `paulBadge.getByRole('button', { name: 'First Name | Contains | Paul', exact: true }).click()`. The dialog `Edit First Name Filter` opens. Change the value to `Pat` and choose `Update Filter`. `queryKit` starts with `firstName @=* Pat`.
- **Remove.** Choose the x button of the Status badge. Run `page.getByRole('button', { name: /^Remove filter: Status \|/ }).click()`. The badge goes away. `queryKit` is `firstName @=* Pat`.
- **Date edit focus.** After `Clear`, choose `Recently Updated`. Choose the badge `Updated Date | Equals | After <date>`. Run `page.getByRole('button', { name: /^Updated Date \| Equals \| After / }).click()`. The dialog `Edit Updated Date Filter` opens. The date control loads lazily, so the dialog first shows the placeholder `status` `Loading filter`. Then the first segment of the date field has the focus: `dialog.getByRole('group', { name: 'Date' }).getByRole('spinbutton').first()`. Press Escape and choose `Clear`.
- **Preset.** Choose `Urgent Bugs`. Run `page.getByRole('button', { name: 'Urgent Bugs' }).click()`. The First Name badge goes away. `queryKit` is `labels ^$ [bug] && labels ^$ [urgent]`.
- **OR.** Choose `Switch to OR`. Run `page.getByRole('button', { name: 'Switch to OR' }).click()`. `queryKit` is `labels ^$ [bug] || labels ^$ [urgent]`. The button name changes to `Switch to AND`.
- **Clear.** Choose `Clear`. Run `page.getByRole('button', { name: 'Clear' }).click()`. The card contains `No filters applied yet` again.
- **Group.** Choose `Urgent Bugs`, then `Group Filters`. The toolbar `Group Filters` button is disabled. A checkbox shows next to each badge. For each `Labels` badge, find the checkbox by its name and click its `label` ancestor: `page.getByRole('checkbox', { name: 'Select filter: Labels | Has | Bug' }).locator('xpath=ancestor::label').click()`. Do the same for `Urgent`. Each checkbox is checked. Choose `Group Selected (2)`. The badge `Group (Depth 1)` shows. `queryKit` is `(labels ^$ [bug] && labels ^$ [urgent])`.
- **Ungroup.** Choose `Ungroup`. The group goes away. `queryKit` is `labels ^$ [bug] && labels ^$ [urgent]`. Choose `Clear`.
- **Two values.** Open the property menu and choose `Status`. In the dialog `Status filter`, choose `Open` and `In Progress`, then `Add (2)`. The badge `Status | In | Open, In Progress` shows. `queryKit` is `status ^^ [open, in-progress]`. Remove the badge.
- **Number filter.** Open the property menu, type `comment`, and choose `Comment Count`. The dialog `Comment Count filter` opens with the operator `Count Equals`. Run `numberFilter.getByRole('spinbutton', { name: 'Value' }).fill('5')`, then choose `Add Filter`. The badge `Comment Count | Count Equals | 5` shows. `queryKit` is `comments #== 5`.
- **Number operator.** Remove the Comment Count badge. Open the `Comment Count filter` submenu again and press Escape. Only the submenu closes: the property menu stays open, and the page still shows the heading `Filter Builder Demo`. Choose `Comment Count` again. Choose the trigger `Count Equals Operator`. The listbox `Operator` opens with `Count Equals` selected. Choose the option `Count Greater Than` (`exact: true`). The trigger name changes to `Count Greater Than Operator`. Type `5` and choose `Add Filter`. The badge `Comment Count | Count Greater Than | 5` shows. `queryKit` is `comments #> 5`.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/filter-builder.mjs`. The captures `text-filter`, `status-submenu`, `preset-or`, `date-edit`, `grouping-selection`, `grouped`, `multiselect-two-values`, `number-filter`, `number-operator-open`, and `number-operator` show the badges, the open submenus, the group, and the QueryKit string.

## Gotchas

- Two buttons are named `Add Filter`: the toolbar button and the submit button of the filter dialog. Scope the second one to `getByRole('dialog', { name: 'Add First Name Filter' })`.
- The property menu has the same name as its trigger, `Add Filter`. Use `getByRole('menu', { name: 'Add Filter' })` for the menu.
- React Aria hides the native checkbox input in the `Status filter` dialog. Click the label text (`getByText('Open', { exact: true })`), then assert the `checkbox` role.
- Text filters are case-insensitive by default. The operator gets a `*` suffix (`@=*`). The `Case sensitive` checkbox removes it.
- A preset replaces the filters. It does not add to them.
- `Switch to OR` shows only when there are two or more filters.
- The ARIA snapshot shows a badge as `First Name | Contains | Paul`, but the DOM has no spaces between its parts. `getByText('First Name | Contains | Paul')` finds nothing. Match the badge with a pattern.
- The filter state is local React state. A reload clears it.
- The grouping checkbox and the x button of a filter use the badge text in their names: `Select filter: Labels | Has | Bug` and `Remove filter: Labels | Has | Bug`. Two filters can have the same property, so match the full text. React Aria hides the native checkbox input, so click its `label` ancestor.
- A group of all the root filters makes the whole string one parenthesized group.
- `Group Filters` is disabled until you select 2 filters. Its name then changes to `Group Selected (2)`.
- Motion animates the height of the sidebar panel in JavaScript. `getAnimations()` does not see it. Read the computed style in each frame.
- The number filter is the last step of the recipe, because a crash of the number dialog stops the page.
- The date control is a lazy chunk (`ui/calendar.tsx` and `@internationalized/date`). The first date submenu or date edit dialog shows the placeholder `getByRole('status', { name: 'Loading filter' })` until the chunk loads. Wait for the date field with `expect`, and capture after it shows. The placeholder has about the size of the date control, so the popover does not jump.
- Without its own overlay state in `components/ui/select.tsx`, the operator Select throws the React Aria error `Duplicate key "#=="` when its submenu closes before the Select ever opened (Escape, a click outside, the pointer on another item, or a new search text). Submit with `Add Filter` does not trigger it. The router error boundary catches the error, so it is a console error and not a `pageerror`. `browse.mjs` fails the run on it. The page shows `Something went wrong!`. The recipe also proves the fix with the heading `Filter Builder Demo` after Escape.
- The operator names are prefixes of each other (`Count Greater Than`, `Count Greater Than or Equal`). Use `exact: true` for an option.

# Components showcase

The Component Library page at `/components` shows each shared UI component with demo data. The user opens the form controls, the overlays (dialog, sheet, drawer, menu, popover, tooltip, command dialog), and toasts, and sees each one respond. The page sends no request to the API.

## Sub-features

- `showcase-select` chooses a value in the `Select (Dropdown)` control.
- `showcase-combobox` filters and chooses a value in the searchable combobox.
- `showcase-autocomplete` chooses a suggestion or keeps free text.
- `showcase-multiselect` chooses more than one value. Each value shows as a chip.
- `showcase-dialog` opens a dialog. On a narrow viewport, the same dialog is a bottom sheet.
- `showcase-sheet` opens a sheet from the right side.
- `showcase-drawer` opens a drawer from the bottom.
- `showcase-menu` opens the `Options` dropdown menu.
- `showcase-popover` opens a calendar in a popover. `Select month` and `Select year` zoom the calendar out. A pick zooms it in again.
- `showcase-tooltip` shows a tooltip on hover.
- `showcase-command` filters the inline command list and the command dialog.
- `showcase-toast` shows a toast and closes it.
- `showcase-collapsible` shows and hides content with `Toggle`.
- `showcase-table` shows the `Users` data grid.
- `showcase-layout` keeps the form controls aligned: the Select fills its column, the date picker is as tall as an Input, and the multi-select chips share one line with the input.
- `showcase-focus` keeps the focus on the page body after the page loads.
- `showcase-avatar` shows the initials of an avatar until its image loads.
- `showcase-sidebar-active` marks only the sidebar items of the current route as active.

## How to get to it (user POV)

- Open `/components`.
- In the sidebar, choose `Examples`, then `Components`.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- The viewport is 1280x800 for all steps except the mobile dialog.

- **Page.** Run `page.goto('/components')`. The heading `Component Library` is visible. `page.getByRole('grid', { name: 'Users' }).getByRole('row')` has 4 rows.
- **Focus on load.** Right after the page load, `page.getByRole('searchbox').first()` does not have the focus. `document.activeElement` is `BODY`.
- **Sidebar active items.** `page.locator('[data-slot="sidebar"] [data-active]')` has 2 elements: `Examples` and `Components`. No element on the page has `data-active="false"` or `data-inset="false"`.
- **Avatar.** Hold the request for the local image `/avatar.png` with `page.route` and reload. The first `[data-slot="avatar"]` shows `CN`, and its image is hidden. Release the request. The image shows, and `CN` is hidden.
- **Layout.** The `Select (Dropdown)` button is as wide as its grid column (1 px tolerance). The group `Start date` is as tall as the textbox `Email`. Both are 32 px.
- **Select.** Run `page.getByRole('button', { name: /Select \(Dropdown\)/ }).click()`, then click the option `Svelte`. The text `Selected: Svelte` shows.
- **Combobox.** Run `fill('vu')` on the combobox `Combobox (Searchable Single Select)`. One option shows. Click `Vue`. The text `Selected: Vue` shows.
- **Autocomplete.** Run `fill('ger')` on the combobox `Autocomplete (Free-form with Suggestions)` and click `Germany`. The text `Value: Germany` shows. Then fill `Atlantis` and press Escape. The text `Value: Atlantis` shows, and the popover is closed. Click `Clear`. The field is empty and keeps the focus. Then press ArrowDown in the empty field. The list shows all 20 countries and stops at its max height of 18rem, so the sweep can check the max height (#218). Escape closes it.
- **Multi-select.** Click the combobox `Select languages...`. Click the options `TypeScript` and `Rust`, then press Escape. The text `Selected: TypeScript, Rust` shows. The grid `Selected items` has 2 rows. After `settle()`, the field `[data-slot="combobox-chips"]` is 32 px tall (1 px tolerance). The vertical center of each chip is within 2 px of the center of the input. Each chip ends to the left of the input.
- **Dialog.** Click `Open Dialog`. The dialog `Dialog Title` shows. Click `Continue`. The dialog closes.
- **Sheet.** Click `Open Sheet`. The dialog `Sheet Title` shows at the right side. Press Escape. The dialog closes.
- **Drawer.** Click `Open Drawer`. The dialog `Drawer Title` shows at the bottom. Click `Done`. The dialog closes. Click `Open Drawer` again, and press Escape at once, while the drawer slides in. The dialog closes, and the focus is on `Open Drawer`.
- **Menu.** Click `Options`. `page.getByRole('menu', { name: 'Options' })` has 3 menu items. No element in the menu has `data-inset`. Click `Profile`. The menu closes.
- **Popover.** Click `Pick a date`. The dialog `Pick a date` shows a calendar grid. Press Escape. The dialog closes.
- **Calendar zoom.** Click `Pick a date`, then `Select month`. The focus is in the listbox `Month picker`, and the popover width does not change (1 px tolerance). ArrowUp (or ArrowDown in the first row) moves the focus by one row of 3 options. Click `Select year`. The focus is in the listbox `Year picker`, and the width does not change. Press Escape. The dialog closes. Open it again, click `Select month` and `Select year`, then click a year option and a month option. The focus is on a day cell. Press Escape. The dialog closes.
- **Tooltip.** Move the mouse, then hover `Notifications`. The tooltip text is `You have 3 notifications`.
- **Inline command.** Run `page.getByRole('searchbox').first().fill('calc')`. The menu item `Calculator` shows. `Calendar` does not show.
- **Command dialog.** Click `Open Command`. Fill `sett` in the searchbox of the dialog. One menu item shows. Press Enter. The dialog closes.
- **Toast.** Click `Success`. A toast with `Changes saved` shows. Click its `Close` button. The toast goes away in less than 1000 ms. If the toast throws a `pageerror`, `browse.mjs` fails the run.
- **Collapsible.** Click `Toggle`. The text `@radix-ui/colors` shows. Click `Toggle` again. The text hides.
- **Mobile dialog.** Set the viewport to 390x844 and click `Open Dialog`. The dialog `Dialog Title` shows as a bottom sheet.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/examples/components-showcase.mjs`. The captures show the page, the form controls, and each open overlay.

## Gotchas

- Capture an open overlay with `capture(name, { fullPage: false })`. The resize for a full-page screenshot closes the Drawer.
- Before you click an option in the multi-select, scroll the input into view and call `settle()`. React Aria closes a popover when the page scrolls, and Playwright can scroll during a click on an element that moves.
- React Aria opens a tooltip on hover only after a pointer move. Run `page.mouse.move(0, 0)` before `hover()`.
- The `Options` menu and the inline command both have the role `menu`. Scope the menu items with `getByRole('menu', { name: 'Options' })`.
- The page has two breadcrumb landmarks: the app header and the demo `Example breadcrumb`. Use `{ name: 'breadcrumb', exact: true }` for the app header.
- The demo toast closes by itself after 5 s. Assert it right after the click. To prove that `Close` works, give the assertion a timeout of 1000 ms. The default timeout lets the toast close by itself.
- The multi-select chips animate in. Call `settle()` before you measure them. A chip in the middle of its animation is smaller than its final size.
- On a fresh load of `/components`, the `Examples` panel of the sidebar is open, because the route is an example route.

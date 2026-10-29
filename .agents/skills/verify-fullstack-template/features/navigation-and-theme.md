# Navigation and theme

The sidebar is on every page. It has links to Home and About, a Demos submenu with Components and Filter Builder, and a theme toggle with light, dark, and system modes. The header has a button that collapses the sidebar to an icon rail, and a breadcrumb with the name of the current page.

## Sub-features

- `nav-links` goes to `/` and `/about` from the sidebar.
- `nav-demos` opens the `Demos` submenu and goes to `/components` and `/filter-demo`.
- `nav-breadcrumb` shows `Home`, `About`, `Components`, or `Filter Builder` for the current route.
- `nav-active` marks only the sidebar items of the current route as active.
- `nav-collapse` collapses the sidebar to icons and expands it again.
- `theme-toggle` sets light, dark, or system. The value stays after a reload.
- `nav-mobile` opens the sidebar as a sheet on a narrow viewport. A link in the sheet goes to its page and closes the sheet.

## How to get to it (user POV)

- Choose a link in the left sidebar.
- Choose the panel icon at the left of the header (`Toggle Sidebar`).
- Choose the sun, moon, or monitor icon at the bottom of the sidebar.
- Press Ctrl+B (Cmd+B on macOS) to collapse or expand the sidebar.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- The browser context is new, so `localStorage` has no `ui-theme` value.

- **Home.** Open `/`. Run `page.goto('/')`. `getByRole('navigation', { name: 'breadcrumb', exact: true })` has the text `Home`. Use `exact: true`, because `/components` also has the landmark `Example breadcrumb`. `page.locator('[data-slot="sidebar"] [data-active]')` has 1 element, `Home`. No element has `data-active="false"`.
- **About.** Choose `About`. Run `page.getByRole('link', { name: 'About' }).click()`. The URL ends with `/about`, the breadcrumb is `About`, and the heading `About` (level 1) shows.
- **Demos.** Open the submenu and choose `Filter Builder`. Run `page.getByRole('button', { name: 'Demos' }).click()`, then `page.getByRole('link', { name: 'Filter Builder' }).click()`. The URL ends with `/filter-demo`, the breadcrumb is `Filter Builder`, and the heading `Filter Builder Demo` is visible.
- **Components.** Choose `Components`. Run `page.getByRole('link', { name: 'Components' }).click()`. The URL ends with `/components` and the breadcrumb is `Components`. The sidebar has 2 active elements: the `Demos` parent and `Components`.
- **Collapse.** Choose the header button. Run `page.getByRole('main').getByRole('button', { name: 'Toggle Sidebar' }).first().click()`. `[data-slot="sidebar"][data-state="collapsed"]` has count 1. Choose it again for `data-state="expanded"`.
- **Dark mode.** Choose `Dark mode`. Run `page.getByRole('button', { name: 'Dark mode' }).click()`. `<html>` has the class `dark`, and `localStorage.getItem('ui-theme')` is `dark`. After `page.reload()`, `<html>` still has the class `dark`.
- **Light and system.** Run `page.getByRole('button', { name: 'Light mode' }).click()`. `<html>` has the class `light`. Then run `page.getByRole('button', { name: 'System theme' }).click()`. `localStorage.getItem('ui-theme')` is `system`.
- **Mobile sheet.** Set the viewport to 390x844. Choose the header button `Toggle Sidebar`. `[data-slot="sidebar"][data-mobile="true"]` shows. It is 288 px (18rem) wide (1 px tolerance) and has no `Close` button. In the sheet, choose `About`. The URL ends with `/about`, the breadcrumb is `About`, and the sheet closes.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/navigation-and-theme.mjs`. The captures `about`, `components`, `collapsed`, `dark`, `mobile-sidebar`, and `mobile-about` show each state.

## Gotchas

- Two elements are named `Toggle Sidebar`: the header button and the sidebar rail. Scope to `getByRole('main')` and use `.first()`.
- The `Demos` submenu is open by default only on a Demos route. On `/` and `/about`, choose `Demos` before you choose its links.
- When the sidebar is collapsed, the theme toggle is wider than the icon rail and its left edge is clipped. This is a known visual defect. Look at the `collapsed` capture.
- In `system` mode, the class on `<html>` follows the color scheme of the browser. Headless Chromium uses light.

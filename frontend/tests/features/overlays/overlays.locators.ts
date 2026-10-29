import type { Page } from '@playwright/test'

export type Overlay = 'Sheet' | 'Drawer' | 'Dialog'

export const overlayDemo = (page: Page) => ({
  heading: page.getByRole('heading', { name: 'Shared components' }),
  openButton: (overlay: Overlay) => page.getByRole('button', { name: `Open ${overlay}`, exact: true }),
  dialog: page.getByRole('dialog'),
  title: page.getByRole('dialog').getByRole('heading'),
  closeButton: page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }),
  devtoolsBadge: page.getByRole('button', { name: 'Open TanStack Router Devtools' }),
})

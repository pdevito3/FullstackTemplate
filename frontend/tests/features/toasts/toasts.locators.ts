import type { Page } from '@playwright/test'

export type ToastButton = 'Success' | 'Error' | 'Info' | 'Warning'

export const toastDemo = (page: Page) => ({
  heading: page.getByRole('heading', { name: 'Component Library' }),
  showToast: (name: ToastButton) => page.getByRole('button', { name, exact: true }),
  region: page.getByRole('region', { name: /notification/ }),
  toasts: page.getByRole('alertdialog'),
})

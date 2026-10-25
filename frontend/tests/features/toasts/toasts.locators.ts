import type { Page } from '@playwright/test'

export type ToastButton = 'Success' | 'Error' | 'Info' | 'Warning'

export const toastDemo = (page: Page) => ({
  heading: page.getByRole('heading', { name: 'Component Library' }),
  showToast: (name: ToastButton) => page.getByRole('button', { name, exact: true }),
  openDialog: page.getByRole('button', { name: 'Open Dialog', exact: true }),
  dialog: page.getByRole('dialog', { name: 'Dialog Title' }),
  dialogTitle: page.getByRole('dialog', { name: 'Dialog Title' }).getByRole('heading', { name: 'Dialog Title' }),
  closeDialog: page.getByRole('dialog', { name: 'Dialog Title' }).getByRole('button', { name: 'Continue' }),
  region: page.getByRole('region', { name: /notification/ }),
  toasts: page.getByRole('alertdialog'),
  toast: (message: string) => page.getByRole('alertdialog').filter({ hasText: message }),
})

import type { Page } from '@playwright/test'

export const session = (page: Page) => ({
  checkFailed: page.getByRole('alert').filter({ hasText: 'We could not check whether you are signed in.' }),
  tryAgain: page.getByRole('button', { name: 'Try Again', exact: true }),
  navigation: page.getByRole('navigation', { name: 'Main' }),
  homeLink: page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Home', exact: true }),
  aboutHeading: page.getByRole('heading', { level: 1, name: 'About' }),
  toasts: page.getByRole('alertdialog'),
})

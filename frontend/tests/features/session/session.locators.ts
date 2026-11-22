import type { Page } from '@playwright/test'

export const session = (page: Page) => ({
  checkFailed: page.getByRole('alert').filter({ hasText: 'We could not check whether you are signed in.' }),
  tryAgain: page.getByRole('button', { name: 'Try Again', exact: true }),
  navigation: page.getByRole('navigation', { name: 'Main' }),
  homeLink: page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Home', exact: true }),
  aboutHeading: page.getByRole('heading', { level: 1, name: 'About' }),
  signInFailedHeading: page.getByRole('heading', { level: 1, name: 'We could not sign you in' }),
  signOutFailedHeading: page.getByRole('heading', { level: 1, name: 'We could not sign you out' }),
  signInAgain: page.getByRole('button', { name: 'Sign in again', exact: true }),
  signOutAgain: page.getByRole('button', { name: 'Sign out again', exact: true }),
  toasts: page.getByRole('alertdialog'),
  rejectedSignInToast: page.getByRole('alertdialog').filter({ hasText: 'The server did not accept your sign-in for this request.' }),
})

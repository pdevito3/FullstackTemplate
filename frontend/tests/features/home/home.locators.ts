import type { Page } from '@playwright/test'

export const home = (page: Page) => ({
  heading: page.getByRole('heading', { level: 1, name: 'Fullstack Template' }),
  notSignedIn: page.getByText('You are not signed in.', { exact: true }),
  signedInAs: (name: string) => page.getByText(`Signed in as ${name}`, { exact: true }),
  userFailed: page.getByText('The Server could not load your user.', { exact: true }),
  login: page.getByRole('button', { name: 'Login', exact: true }),
  logout: page.getByRole('button', { name: 'Logout', exact: true }),
})

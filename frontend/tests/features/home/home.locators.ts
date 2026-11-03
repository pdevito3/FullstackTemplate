import type { Page } from '@playwright/test'

export const home = (page: Page) => ({
  heading: page.getByRole('heading', { level: 1, name: 'Fullstack Template' }),
  signedInAs: (name: string) => page.getByText(`Signed in as ${name}`, { exact: true }),
  userFailed: page.getByText('The Server could not load your user.', { exact: true }),
  logout: page.getByRole('button', { name: 'Logout', exact: true }),
})

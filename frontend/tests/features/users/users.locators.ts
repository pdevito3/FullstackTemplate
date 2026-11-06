import type { Page } from '@playwright/test'

export const users = (page: Page) => {
  const grid = page.getByRole('grid', { name: 'Users' })
  const roleDialog = page.getByRole('dialog', { name: /^Change the role of / })
  const deleteDialog = page.getByRole('dialog', { name: /^Delete .+\?$/ })
  const details = page.getByRole('region', { name: 'Details' })
  const startTenantFirst = page.getByText('Start a tenant to manage its users.', { exact: true })

  return {
    listHeading: page.getByRole('heading', { name: 'Users', exact: true }),
    usersNavLink: page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Users', exact: true }),
    grid,
    row: (name: string) => grid.getByRole('row', { name: new RegExp(`^${name}`) }),
    cell: (name: string, text: string) =>
      grid.getByRole('row', { name: new RegExp(`^${name}`) }).getByRole('gridcell', { name: text, exact: true }),
    nameLink: (name: string) => grid.getByRole('link', { name, exact: true }),
    nextPage: page.getByRole('navigation', { name: 'Pages of users' }).getByRole('button', { name: 'Next' }),
    emptyPage: page.getByText(/^There are no users on page \d+\./),
    firstPageLink: page.getByRole('link', { name: 'Go to page 1' }),
    loadFailure: page.getByRole('alert').filter({ hasText: 'The users did not load.' }),
    startTenantFirst,
    startTenantLink: startTenantFirst.getByRole('link', { name: 'Start a tenant' }),
    detailHeading: (name: string) => page.getByRole('heading', { name, exact: true }),
    loadingUser: page.getByRole('status', { name: 'Loading user' }),
    detail: (text: string) => details.getByRole('definition').and(details.getByText(text, { exact: true })),
    actions: (name: string) => page.getByRole('group', { name: `Actions for ${name}` }),
    actionButtons: (name: string) => page.getByRole('group', { name: `Actions for ${name}` }).getByRole('button'),
    actionButton: (name: string, label: string) =>
      page.getByRole('group', { name: `Actions for ${name}` }).getByRole('button', { name: label, exact: true }),
    roleDialog,
    roleSelect: roleDialog.getByRole('button', { name: / Role$/ }),
    roleOption: (label: string) => page.getByRole('option', { name: label, exact: true }),
    keepRole: roleDialog.getByRole('button', { name: 'Keep role', exact: true }),
    confirmRole: roleDialog.getByRole('button', { name: 'Change role', exact: true }),
    deleteDialog,
    keepUser: deleteDialog.getByRole('button', { name: 'Keep user', exact: true }),
    confirmDelete: deleteDialog.getByRole('button', { name: 'Delete user', exact: true }),
    toast: (text: string) => page.getByRole('alertdialog').filter({ hasText: text }),
    toasts: page.getByRole('alertdialog'),
    sentence: (text: string) => page.getByText(text, { exact: true }),
  }
}

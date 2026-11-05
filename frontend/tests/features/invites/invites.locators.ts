import type { Page } from '@playwright/test'

export const invites = (page: Page) => {
  const inviteDialog = page.getByRole('dialog', { name: 'Invite a user' })
  const linkDialog = page.getByRole('dialog', { name: 'Send this link to your colleague' })
  const revokeDialog = page.getByRole('dialog', { name: /^Revoke / })
  const grid = page.getByRole('grid', { name: 'Invites' })
  const row = (email: string) => grid.getByRole('row', { name: new RegExp(`^${email}`) })

  return {
    usersHeading: page.getByRole('heading', { name: 'Users', exact: true }),
    usersGrid: page.getByRole('grid', { name: 'Users' }),
    inviteButton: page.getByRole('button', { name: 'Invite a user', exact: true }),
    inviteDialog,
    roleSelect: inviteDialog.getByRole('button', { name: / Role$/ }),
    roleOption: (label: string) => page.getByRole('option', { name: label, exact: true }),
    emailField: inviteDialog.getByRole('textbox', { name: 'Email (optional)' }),
    emailMatch: inviteDialog.getByText('Require email match', { exact: true }),
    fieldError: (text: string) => inviteDialog.getByRole('alert').filter({ hasText: text }),
    createLink: inviteDialog.getByRole('button', { name: 'Create link', exact: true }),
    cancelInvite: inviteDialog.getByRole('button', { name: 'Cancel', exact: true }),
    closeInvite: inviteDialog.getByRole('button', { name: 'Close', exact: true }),
    linkDialog,
    linkField: linkDialog.getByRole('textbox', { name: 'Invite link' }),
    copyLink: linkDialog.getByRole('button', { name: 'Copy link', exact: true }),
    linkCopied: linkDialog.getByRole('status').filter({ hasText: 'Link copied.' }),
    onlyTimeSentence: linkDialog.getByText('This is the only time that this link shows, because the app stores only a hash of it.', { exact: true }),
    linkExpiry: linkDialog.getByText(/^One person can use this link, until /),
    done: linkDialog.getByRole('button', { name: 'Done', exact: true }),
    invitesHeading: page.getByRole('heading', { name: 'Invites', exact: true }),
    grid,
    row,
    cell: (email: string, text: string) => row(email).getByRole('gridcell').filter({ hasText: text }),
    rowEmailNote: (email: string, text: string) => row(email).getByText(text, { exact: true }),
    revokeOf: (email: string) => row(email).getByRole('button', { name: /^Revoke the invite for / }),
    empty: page.getByText('There are no invites. Choose Invite a user to make a link.', { exact: true }),
    nextPage: page.getByRole('navigation', { name: 'Pages of invites' }).getByRole('button', { name: 'Next' }),
    loadFailure: page.getByRole('alert').filter({ hasText: 'The invites did not load.' }),
    tenantFailure: page.getByRole('alert').filter({ hasText: 'The tenant did not load.' }),
    tenantRetry: page.getByRole('alert').filter({ hasText: 'The tenant did not load.' }).getByRole('button', { name: 'Try again', exact: true }),
    usersNavLink: page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Users', exact: true }),
    userLink: (name: string) => page.getByRole('grid', { name: 'Users' }).getByRole('link', { name, exact: true }),
    revokeDialog,
    keepInvite: revokeDialog.getByRole('button', { name: 'Keep invite', exact: true }),
    confirmRevoke: revokeDialog.getByRole('button', { name: 'Revoke invite', exact: true }),
    toast: (text: string) => page.getByRole('alertdialog').filter({ hasText: text }),
    toasts: page.getByRole('alertdialog'),
  }
}

export const join = (page: Page) => {
  const details = page.getByRole('region', { name: 'Invite' })
  const form = page.getByRole('form', { name: 'Join the tenant' })

  return {
    heading: page.getByRole('heading', { name: 'Join a tenant', exact: true }),
    detail: (text: string) => details.getByRole('definition').and(details.getByText(text, { exact: true })),
    details,
    form,
    firstName: form.getByRole('textbox', { name: 'First name' }),
    lastName: form.getByRole('textbox', { name: 'Last name' }),
    email: form.getByRole('textbox', { name: 'Email' }),
    username: form.getByRole('textbox', { name: 'Username' }),
    emailNote: form.getByText('This invite is for one email address, so the app uses the email of your account.', { exact: true }),
    formAlert: form.getByRole('alert'),
    submit: form.getByRole('button', { name: 'Join tenant', exact: true }),
    alert: (text: string) => page.getByRole('alert').filter({ hasText: text }),
    alreadyMember: page.getByText('Your account already belongs to a tenant.', { exact: true }),
    homeLink: page.getByRole('link', { name: 'Go to the home page', exact: true }),
    retry: page.getByRole('button', { name: 'Try again', exact: true }),
    homeHeading: page.getByRole('heading', { name: 'Fullstack Template', exact: true }),
    aboutHeading: page.getByRole('heading', { level: 1 }),
    startTenantNotice: page.getByRole('link', { name: 'Start a tenant' }),
    toast: (text: string) => page.getByRole('alertdialog').filter({ hasText: text }),
    toasts: page.getByRole('alertdialog'),
  }
}

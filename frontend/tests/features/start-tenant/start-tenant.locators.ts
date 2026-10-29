import type { Page } from '@playwright/test'

export const startTenant = (page: Page) => {
  const notice = page.getByRole('region', { name: 'No tenant' })
  const form = page.getByRole('form', { name: 'Start a tenant' })

  return {
    notice,
    noticeLink: notice.getByRole('link', { name: 'Start a tenant' }),
    heading: page.getByRole('heading', { name: 'Start a tenant', level: 1 }),
    form,
    field: (label: string) => form.getByLabel(label, { exact: true }),
    submit: form.getByRole('button', { name: 'Start tenant' }),
    formErrors: form.getByRole('alert'),
    alreadyMember: page.getByText(/^You are already a member of a tenant\./),
    logInPrompt: page.getByText('Log in to start a tenant.'),
    login: page.getByRole('button', { name: 'Login', exact: true }),
    started: (tenantName: string) => page.getByText(`You started ${tenantName}. You are its admin`, { exact: false }),
    homeLink: page.getByRole('main').getByRole('link', { name: 'Go to the home page' }),
    lookupFailure: page.getByRole('main').getByRole('alert'),
    tryAgain: page.getByRole('button', { name: 'Try Again' }),
    toasts: page.getByRole('alertdialog'),
  }
}

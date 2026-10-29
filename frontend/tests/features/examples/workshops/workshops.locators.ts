import type { Page } from '@playwright/test'

export const workshops = (page: Page) => {
  const editForm = page.getByRole('form', { name: 'Edit workshop' })
  const staleAlert = editForm.getByRole('alert').filter({ hasText: 'Someone else saved this workshop' })

  return {
    listHeading: page.getByRole('heading', { name: 'Workshops', exact: true }),
    search: page.getByRole('searchbox', { name: 'Search workshops' }),
    row: (title: string) => page.getByRole('grid', { name: 'Workshops' }).getByRole('row', { name: new RegExp(`^${title}`) }),
    titleLink: (title: string) => page.getByRole('link', { name: title, exact: true }),
    pager: page.getByRole('navigation', { name: 'Pages of workshops' }),
    nextPage: page.getByRole('navigation', { name: 'Pages of workshops' }).getByRole('button', { name: 'Next' }),
    detailHeading: (title: string) => page.getByRole('heading', { name: title, exact: true }),
    details: page.getByRole('region', { name: 'Details' }),
    status: (status: string) => page.getByText(status, { exact: true }),
    toast: (text: string) => page.getByRole('alertdialog').filter({ hasText: text }),
    actions: (title: string) => page.getByRole('group', { name: `Actions for ${title}` }),
    actionButton: (title: string, name: string) =>
      page.getByRole('group', { name: `Actions for ${title}` }).getByRole('button', { name, exact: true }),
    editLink: (title: string) => page.getByRole('group', { name: `Actions for ${title}` }).getByRole('link', { name: 'Edit' }),
    editForm,
    title: editForm.getByRole('textbox', { name: 'Title' }),
    save: editForm.getByRole('button', { name: 'Save', exact: true }),
    staleAlert,
    loadLatest: staleAlert.getByRole('button', { name: 'Load the latest' }),
  }
}

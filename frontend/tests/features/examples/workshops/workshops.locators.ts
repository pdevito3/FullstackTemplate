import type { Page } from '@playwright/test'

export const workshops = (page: Page) => {
  const editForm = page.getByRole('form', { name: 'Edit workshop' })
  const newForm = page.getByRole('form', { name: 'New workshop' })
  const venueForm = page.getByRole('form', { name: 'New venue' })
  const newStart = newForm.getByRole('group', { name: 'Starts at' })
  const editStart = editForm.getByRole('group', { name: 'Starts at' })
  const cancelDialog = page.getByRole('dialog', { name: 'Cancel this workshop?' })
  const staleAlert = editForm.getByRole('alert').filter({ hasText: 'This workshop changed after you opened it.' })

  return {
    listHeading: page.getByRole('heading', { name: 'Workshops', exact: true }),
    search: page.getByRole('searchbox', { name: 'Search workshops' }),
    grid: page.getByRole('grid', { name: 'Workshops' }),
    row: (title: string) => page.getByRole('grid', { name: 'Workshops' }).getByRole('row', { name: new RegExp(`^${title}`) }),
    rowStatus: (title: string, status: string) =>
      page.getByRole('grid', { name: 'Workshops' }).getByRole('row', { name: new RegExp(`^${title}`) }).getByText(status, { exact: true }),
    titleLink: (title: string) => page.getByRole('link', { name: title, exact: true }),
    pager: page.getByRole('navigation', { name: 'Pages of workshops' }),
    nextPage: page.getByRole('navigation', { name: 'Pages of workshops' }).getByRole('button', { name: 'Next' }),
    editHeading: page.getByRole('heading', { name: 'Edit workshop', exact: true }),
    editRefusal: (text: string) => page.getByText(text, { exact: true }),
    detailHeading: (title: string) => page.getByRole('heading', { name: title, exact: true }),
    details: page.getByRole('region', { name: 'Details' }),
    status: (status: string) => page.getByText(status, { exact: true }),
    toast: (text: string) => page.getByRole('alertdialog').filter({ hasText: text }),
    toasts: page.getByRole('alertdialog'),
    emptyPage: page.getByText(/^There are no workshops on page \d+\./),
    firstPageLink: page.getByRole('link', { name: 'Go to page 1' }),
    actions: (title: string) => page.getByRole('group', { name: `Actions for ${title}` }),
    actionButton: (title: string, name: string) =>
      page.getByRole('group', { name: `Actions for ${title}` }).getByRole('button', { name, exact: true }),
    cancelDialog,
    confirmCancel: cancelDialog.getByRole('button', { name: 'Cancel workshop', exact: true }),
    keepWorkshop: cancelDialog.getByRole('button', { name: 'Keep workshop', exact: true }),
    editLink: (title: string) => page.getByRole('group', { name: `Actions for ${title}` }).getByRole('link', { name: 'Edit' }),
    newHeading: page.getByRole('heading', { name: 'New workshop', exact: true }),
    newForm,
    newTitle: newForm.getByRole('textbox', { name: 'Title' }),
    newStart,
    newStartSegments: newStart.getByRole('spinbutton'),
    newStartMonth: newStart.getByRole('spinbutton', { name: /^month/ }),
    newStartZone: (zone: string) => newForm.getByText(`The time in ${zone}.`, { exact: true }),
    newStartWithNoZone: newForm.getByText('The time at the venue that you pick.', { exact: true }),
    newVenue: newForm.getByRole('combobox', { name: 'Venue' }),
    venueOption: (name: string) => page.getByRole('option', { name, exact: true }),
    newFormMessage: (text: string) => newForm.getByText(text, { exact: true }),
    newCapacity: newForm.getByRole('spinbutton', { name: 'Capacity' }),
    add: newForm.getByRole('button', { name: 'Add workshop' }),
    venueForm,
    venueName: venueForm.getByRole('textbox', { name: 'Name' }),
    addVenue: venueForm.getByRole('button', { name: 'Add venue' }),
    editForm,
    title: editForm.getByRole('textbox', { name: 'Title' }),
    editStart,
    editStartSegments: editStart.getByRole('spinbutton'),
    editStartHour: editStart.getByRole('spinbutton', { name: /^hour/ }),
    editStartZone: (zone: string) => editForm.getByText(`The time in ${zone}.`, { exact: true }),
    save: editForm.getByRole('button', { name: 'Save', exact: true }),
    staleAlert,
    loadLatest: staleAlert.getByRole('button', { name: 'Load the latest' }),
    newWorkshopLink: page.getByRole('main').getByRole('link', { name: 'New workshop', exact: true }),
    newVenueLink: page.getByRole('main').getByRole('link', { name: 'New venue', exact: true }),
    emptyList: page.getByText('No workshops yet. Add one with New workshop.', { exact: true }),
    startTenantFirst: page.getByText('Start a tenant to add workshops.', { exact: true }),
    startTenantLink: page.getByText('Start a tenant to add workshops.', { exact: true }).getByRole('link', { name: 'Start a tenant' }),
  }
}

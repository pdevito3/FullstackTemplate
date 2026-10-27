import type { Page } from '@playwright/test'

export const filterDemo = (page: Page) => {
  const dialog = (name: string) => page.getByRole('dialog', { name, exact: true })
  // The Add Filter submenu names its dialog. The Add and Edit dialogs take their name from the title.
  const control = (propertyLabel: string) =>
    dialog(`${propertyLabel} filter`).or(dialog(`Add ${propertyLabel} Filter`)).or(dialog(`Edit ${propertyLabel} Filter`))

  return {
    heading: page.getByRole('heading', { name: 'Filter Builder Demo' }),
    addFilterMenu: page.getByRole('button', { name: 'Add Filter', exact: true }),
    searchProperties: page.getByRole('searchbox', { name: 'Search properties' }),
    property: (propertyLabel: string) => page.getByRole('menuitem', { name: propertyLabel, exact: true }),
    control,
    loadingControl: page.getByRole('status', { name: 'Loading filter' }),
    valueGroup: (propertyLabel: string) => control(propertyLabel).getByRole('group', { name: propertyLabel, exact: true }),
    controlLabel: (propertyLabel: string) => control(propertyLabel).getByText(propertyLabel, { exact: true }),
    button: (propertyLabel: string, name: string) => control(propertyLabel).getByRole('button', { name, exact: true }),
    fields: (propertyLabel: string) =>
      control(propertyLabel).getByRole('button').or(control(propertyLabel).getByRole('textbox')),
    pressedButtons: (propertyLabel: string) => control(propertyLabel).getByRole('button', { pressed: true }),
    dateField: (propertyLabel: string, name: string) => control(propertyLabel).getByRole('group', { name, exact: true }),
    calendar: (propertyLabel: string) => control(propertyLabel).getByRole('grid'),
    addFilter: (propertyLabel: string) => control(propertyLabel).getByRole('button', { name: 'Add Filter', exact: true }),
    editFilter: (propertyLabel: string) => page.getByRole('button', { name: new RegExp(`^${propertyLabel} \\|`) }),
    removeFilter: (propertyLabel: string) => page.getByRole('button', { name: new RegExp(`^Remove filter: ${propertyLabel}`) }),
  }
}

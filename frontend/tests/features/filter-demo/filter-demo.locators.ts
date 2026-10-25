import type { Page } from '@playwright/test'

export const filterDemo = (page: Page) => {
  const control = (propertyLabel: string) => page.getByRole('dialog', { name: `${propertyLabel} filter` })

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
    pressedButtons: (propertyLabel: string) => control(propertyLabel).getByRole('button', { pressed: true }),
    dateField: (propertyLabel: string, name: string) => control(propertyLabel).getByRole('group', { name, exact: true }),
    calendar: (propertyLabel: string) => control(propertyLabel).getByRole('grid'),
    addFilter: (propertyLabel: string) => control(propertyLabel).getByRole('button', { name: 'Add Filter', exact: true }),
    editFilter: (propertyLabel: string) => page.getByRole('button', { name: new RegExp(`^${propertyLabel} \\|`) }),
    removeFilter: (propertyLabel: string) => page.getByRole('button', { name: new RegExp(`^Remove filter: ${propertyLabel}`) }),
  }
}

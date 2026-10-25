import type { Page } from '@playwright/test'

export const filterDemo = (page: Page) => {
  const control = (propertyLabel: string) => page.getByRole('dialog', { name: new RegExp(`${propertyLabel} filter$`, 'i') })

  return {
    heading: page.getByRole('heading', { name: 'Filter Builder Demo' }),
    addFilterMenu: page.getByRole('button', { name: 'Add Filter', exact: true }),
    property: (propertyLabel: string) => page.getByRole('menuitem', { name: propertyLabel, exact: true }),
    control,
    loadingControl: page.getByRole('status', { name: 'Loading filter' }),
    quickSelect: (propertyLabel: string, name: string) => control(propertyLabel).getByRole('button', { name, exact: true }),
    addFilter: (propertyLabel: string) => control(propertyLabel).getByRole('button', { name: 'Add Filter', exact: true }),
    removeFilter: (propertyLabel: string) => page.getByRole('button', { name: new RegExp(`^Remove filter: ${propertyLabel}`) }),
  }
}

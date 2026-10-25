import type { Page } from '@playwright/test'

/** `property` is the label of a filter property, for example `Created Date`. */
export const filterDemo = (page: Page) => {
  const control = (property: string) => page.getByRole('dialog', { name: new RegExp(`${property} filter$`, 'i') })

  return {
    heading: page.getByRole('heading', { name: 'Filter Builder Demo' }),
    addFilterMenu: page.getByRole('button', { name: 'Add Filter', exact: true }),
    property: (property: string) => page.getByRole('menuitem', { name: property, exact: true }),
    control,
    loadingControl: page.getByRole('status', { name: 'Loading filter' }),
    quickSelect: (property: string, name: string) => control(property).getByRole('button', { name, exact: true }),
    addFilter: (property: string) => control(property).getByRole('button', { name: 'Add Filter', exact: true }),
    removeFilter: (property: string) => page.getByRole('button', { name: new RegExp(`^Remove filter: ${property}`) }),
  }
}

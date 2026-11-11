import type { Page } from '@playwright/test'

export const filterBuilderDemo = (page: Page) => {
  const dialog = (name: string) => page.getByRole('dialog', { name, exact: true })
  // The Add Filter submenu names its dialog. The Add dialog of a phone takes its name from the title.
  const control = (propertyLabel: string) => dialog(`${propertyLabel} filter`).or(dialog(`Add ${propertyLabel} Filter`))

  return {
    heading: page.getByRole('heading', { name: 'Shared components' }),
    pageError: page.getByRole('heading', { name: 'This page did not load' }),
    addFilterMenu: page.getByRole('button', { name: 'Add Filter', exact: true }),
    property: (propertyLabel: string) => page.getByRole('menuitem', { name: propertyLabel, exact: true }),
    control,
    loadError: (propertyLabel: string) => control(propertyLabel).getByRole('alert'),
    tryAgain: (propertyLabel: string) => control(propertyLabel).getByRole('button', { name: 'Try again', exact: true }),
    dateSegment: (propertyLabel: string, part: 'month' | 'day' | 'year') =>
      control(propertyLabel).getByRole('spinbutton', { name: `${part}, Date`, exact: true }),
  }
}

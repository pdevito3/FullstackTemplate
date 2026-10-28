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
    controlTitle: (propertyLabel: string) =>
      control(propertyLabel).getByRole('heading', { name: new RegExp(`^(Add|Edit) ${propertyLabel} Filter$`) }),
    controlLabel: (propertyLabel: string) => control(propertyLabel).getByText(propertyLabel, { exact: true }),
    button: (propertyLabel: string, name: string) => control(propertyLabel).getByRole('button', { name, exact: true }),
    fields: (propertyLabel: string) =>
      control(propertyLabel).getByRole('button').or(control(propertyLabel).getByRole('textbox')),
    pressedButtons: (propertyLabel: string) => control(propertyLabel).getByRole('button', { pressed: true }),
    dateField: (propertyLabel: string, name: string) => control(propertyLabel).getByRole('group', { name, exact: true }),
    dateSegment: (propertyLabel: string, field: string, part: 'month' | 'day' | 'year') =>
      control(propertyLabel).getByRole('spinbutton', { name: `${part}, ${field}`, exact: true }),
    dateMessage: (propertyLabel: string, text: string) => control(propertyLabel).getByText(text, { exact: true }),
    calendar: (propertyLabel: string) => control(propertyLabel).getByRole('grid'),
    calendarNav: (propertyLabel: string, name: 'Next' | 'Previous') =>
      control(propertyLabel).getByRole('banner').getByRole('button', { name, exact: true }),
    selectMonth: (propertyLabel: string) => control(propertyLabel).getByRole('button', { name: 'Select month', exact: true }),
    monthOption: (propertyLabel: string, name: string) =>
      control(propertyLabel).getByRole('listbox', { name: 'Month picker' }).getByRole('option', { name, exact: true }),
    addFilter: (propertyLabel: string) => control(propertyLabel).getByRole('button', { name: 'Add Filter', exact: true }),
    addSelected: (propertyLabel: string) => control(propertyLabel).getByRole('button', { name: /^Add \(\d+\)$/ }),
    option: (propertyLabel: string, name: string) => control(propertyLabel).getByRole('checkbox', { name, exact: true }),
    onlyButtons: (propertyLabel: string) => control(propertyLabel).getByRole('button', { name: /^Only / }),
    toasts: page.getByRole('alertdialog'),
    editFilter: (propertyLabel: string) => page.getByRole('button', { name: new RegExp(`^${propertyLabel} \\|`) }),
    removeFilter: (propertyLabel: string) => page.getByRole('button', { name: new RegExp(`^Remove filter: ${propertyLabel}`) }),
  }
}

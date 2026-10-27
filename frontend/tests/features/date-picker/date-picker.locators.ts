import type { Page } from '@playwright/test'

export const datePickerDemo = (page: Page) => {
  const picker = (label: string) => page.getByRole('group', { name: label, exact: true })

  return {
    heading: page.getByRole('heading', { name: 'Component Library' }),
    picker,
    segment: (label: string, part: 'month' | 'day' | 'year') =>
      picker(label).getByRole('spinbutton', { name: `${part}, ${label}`, exact: true }),
    message: (text: string) => page.getByText(text, { exact: true }),
    acceptTerms: page.getByRole('checkbox', { name: 'Accept terms and conditions' }),
  }
}

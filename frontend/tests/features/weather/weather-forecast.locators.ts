import type { Page } from '@playwright/test'

/** `label` is the date of a day as the page shows it, for example `Sat, Mar 14`. */
export const weatherForecast = (page: Page) => {
  const day = (label: string) => page.getByRole('listitem', { name: label, exact: true })

  return {
    heading: page.getByRole('heading', { name: 'Weather Forecast' }),
    days: page.getByRole('list', { name: 'Forecast' }).getByRole('listitem'),
    day,
    temperature: (label: string) => day(label).getByText(/^-?\d+°$/),
    scale: (label: string) => day(label).getByText(/^(Celsius|Fahrenheit)$/),
    celsius: page.getByRole('button', { name: '°C', exact: true }),
    refresh: page.getByRole('button', { name: 'Refresh', exact: true }),
    tryAgain: page.getByRole('button', { name: 'Try Again', exact: true }),
    loading: page.getByRole('status', { name: 'Loading forecast' }),
    emptyMessage: page.getByText('No weather data available'),
    error: page.getByRole('alert').filter({ hasText: 'Error loading weather data' }),
    toast: (text: string) => page.getByRole('alertdialog').filter({ hasText: text }),
    login: page.getByRole('button', { name: 'Login', exact: true }),
    logout: page.getByRole('button', { name: 'Logout', exact: true }),
    welcome: (name: string) => page.getByText(`Welcome, ${name}`, { exact: true }),
    secureToggle: page.getByRole('button', { name: /^(Public|Secure) API$/ }),
    authenticatedNotice: page.getByText('Showing authenticated data', { exact: true }),
  }
}

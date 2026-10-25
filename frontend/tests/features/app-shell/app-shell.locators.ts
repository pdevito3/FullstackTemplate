import type { Page } from '@playwright/test'

export const appShell = (page: Page) => ({
  aboutText: page.getByText('This is a demo application built with .NET Aspire and React'),
  currentPage: page.getByRole('navigation', { name: 'breadcrumb' }).getByRole('link'),
  notFoundHeading: page.getByRole('heading', { name: 'Page not found' }),
  homeLink: page.getByRole('link', { name: 'Go to the home page' }),
  weatherHeading: page.getByRole('heading', { name: 'Weather Forecast' }),
})

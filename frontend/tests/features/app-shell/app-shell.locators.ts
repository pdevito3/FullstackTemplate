import type { Page } from '@playwright/test'

export const appShell = (page: Page) => ({
  aboutHeading: page.getByRole('heading', { level: 1, name: 'About' }),
  aboutText: page.getByText('This is a demo application built with .NET Aspire and React'),
  currentPage: page.getByRole('navigation', { name: 'breadcrumb' }).getByRole('link'),
  contentInfoLandmarks: page.getByRole('contentinfo'),
  // The About page has no separator of its own, so the one separator in main is the one of the layout header.
  headerSeparator: page.getByRole('main').getByRole('separator'),
  routerDevtoolsButton: page.getByRole('button', { name: 'Open TanStack Router Devtools' }),
  mainNavigation: page.getByRole('navigation', { name: 'Main' }),
  mobileSidebar: page.locator('[data-slot="sidebar"][data-mobile="true"]'),
  sidebarTrigger: page.getByRole('main').getByRole('button', { name: 'Toggle Sidebar' }),
  mainLandmarks: page.getByRole('main'),
  wideContent: page.getByRole('region', { name: 'Wide content' }),
  notFoundHeading: page.getByRole('heading', { name: 'Page not found' }),
  homeLink: page.getByRole('link', { name: 'Go to the home page' }),
  weatherHeading: page.getByRole('heading', { name: 'Weather Forecast' }),
})

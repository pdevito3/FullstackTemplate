import type { Page } from '@playwright/test'

// The split chunk of the About page component. A failure of this chunk stops the page, not the app.
export const aboutPageSource = /\/assets\/about-[\w-]+\.js$/

export const appShell = (page: Page) => ({
  aboutHeading: page.getByRole('heading', { level: 1, name: 'About' }),
  aboutText: page.getByText('This app runs on .NET Aspire and React.'),
  currentPage: page.getByRole('navigation', { name: 'breadcrumb' }).getByRole('link'),
  contentInfoLandmarks: page.getByRole('contentinfo'),
  // The About page has no separator of its own, so the one separator in main is the one of the layout header.
  headerSeparator: page.getByRole('main').getByRole('separator'),
  routerDevtoolsButton: page.getByRole('button', { name: 'Open TanStack Router Devtools' }),
  mobileSidebar: page.locator('[data-slot="sidebar"][data-mobile="true"]'),
  mobileSidebarAppName: page.locator('[data-slot="sidebar"][data-mobile="true"]').getByText('Fullstack Template', { exact: true }),
  homeNavigationLink: page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Home' }),
  aboutNavigationLink: page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'About' }),
  jobsDashboardLink: page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Background jobs' }),
  navigation: page.getByRole('navigation', { name: 'Main' }),
  currentNavigationItems: page.getByRole('navigation', { name: 'Main' }).locator('[aria-current]'),
  sidebarTrigger: page.getByRole('main').getByRole('button', { name: 'Toggle Sidebar' }),
  mainLandmarks: page.getByRole('main'),
  wideContent: page.getByRole('region', { name: 'Wide content' }),
  notFoundHeading: page.getByRole('heading', { name: 'Page not found' }),
  pageError: page.getByRole('main').getByRole('alert'),
  reloadPage: page.getByRole('button', { name: 'Reload the page' }),
  homeLink: page.getByRole('link', { name: 'Go to the home page' }),
  homeSubtitle: page.getByText('.NET Aspire + React with a BFF'),
  pageAnnouncement: page.getByTestId('page-announcement'),
})

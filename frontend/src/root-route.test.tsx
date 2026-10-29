import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  RouterProvider,
  type AnyRoute,
} from '@tanstack/react-router'
import { expect, test } from 'vitest'
import { render } from 'vitest-browser-react'

import { AuthKeys } from '@/domain/auth/apis/auth.keys'
import { Route as RootRoute } from '@/routes/__root'
import { routeTree } from '@/routeTree.gen'

async function renderLayoutAt(path: string) {
  const rootRoute = createRootRoute({ component: RootRoute.options.component })
  const userRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/users/$id',
    staticData: { breadcrumb: 'User' },
    component: () => <h1>User</h1>,
  })
  const router = createRouter({
    routeTree: rootRoute.addChildren([userRoute]),
    history: createMemoryHistory({ initialEntries: [path] }),
  })
  // Seed an anonymous session, so that the session and membership queries of the layout send no request.
  const queryClient = new QueryClient()
  queryClient.setQueryData(AuthKeys.user(), null)
  const screen = await render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  await expect.element(screen.getByRole('heading', { name: 'User' })).toBeVisible()
  return screen
}

test('the breadcrumb of a route with params shows the name that the route declares', async () => {
  const screen = await renderLayoutAt('/users/u_1')

  await expect.element(screen.getByRole('navigation', { name: 'breadcrumb' }).getByRole('link')).toHaveTextContent('User')
})

test('each page of the app declares the name that its breadcrumb shows', () => {
  const router = createRouter({ routeTree })
  // A pathless layout or a route with an index child is never the last match, so a child names the page.
  const hasIndexChild = (route: AnyRoute) =>
    Object.values((route.children ?? {}) as Record<string, AnyRoute>).some((child) => child.path === '/')

  const pagesWithoutBreadcrumb = Object.values(router.routesById)
    .filter((route) => !route.isRoot && route.path !== undefined && !hasIndexChild(route))
    .filter((route) => !route.options.staticData?.breadcrumb)
    .map((route) => route.fullPath)

  expect(Object.keys(router.routesById).length).toBeGreaterThan(1)
  expect(pagesWithoutBreadcrumb).toEqual([])
})

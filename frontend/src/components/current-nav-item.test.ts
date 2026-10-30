import { createMemoryHistory, createRootRoute, createRoute, createRouter } from '@tanstack/react-router'
import { describe, expect, it } from 'vitest'

import { currentNavItem } from '@/components/current-nav-item'

const rootRoute = createRootRoute()
const routeTree = rootRoute.addChildren(
  ['/', '/about', '/projects', '/projects/new', '/projects/$id', '/projects/$id/edit'].map((path) =>
    createRoute({ getParentRoute: () => rootRoute, path }),
  ),
)

async function currentAt(path: string, urls: readonly string[]) {
  const router = createRouter({ routeTree, history: createMemoryHistory({ initialEntries: [path] }) })
  await router.load()
  return currentNavItem(urls, (to, fuzzy) => router.matchRoute({ to }, { fuzzy }) !== false)
}

const navUrls = ['/', '/about', '/projects']

describe('currentNavItem', () => {
  it('gives the item of the current page as the page', async () => {
    expect(await currentAt('/projects', navUrls)).toEqual({ url: '/projects', current: 'page' })
  })

  it('gives the item of a page under the current page as the current item, not the page', async () => {
    expect(await currentAt('/projects/prj_1', navUrls)).toEqual({ url: '/projects', current: 'true' })
    expect(await currentAt('/projects/prj_1/edit', navUrls)).toEqual({ url: '/projects', current: 'true' })
  })

  it('ignores the search of the location', async () => {
    expect(await currentAt('/projects?page=2', navUrls)).toEqual({ url: '/projects', current: 'page' })
  })

  it('does not match an item to a path that only starts with the same text', async () => {
    expect(await currentAt('/projectsx', navUrls)).toBeUndefined()
  })

  it('gives the / item only on the home page', async () => {
    expect(await currentAt('/', navUrls)).toEqual({ url: '/', current: 'page' })
    expect(await currentAt('/start-tenant', navUrls)).toBeUndefined()
    expect(await currentAt('/about', navUrls)).toEqual({ url: '/about', current: 'page' })
  })

  it('gives the most specific item when two items match, in each order of the items', async () => {
    const urls = ['/projects', '/projects/new']

    expect(await currentAt('/projects/new', urls)).toEqual({ url: '/projects/new', current: 'page' })
    expect(await currentAt('/projects/new', [...urls].reverse())).toEqual({ url: '/projects/new', current: 'page' })
    expect(await currentAt('/projects/new/step', urls)).toEqual({ url: '/projects/new', current: 'true' })
    expect(await currentAt('/projects/new/step', [...urls].reverse())).toEqual({ url: '/projects/new', current: 'true' })
  })
})

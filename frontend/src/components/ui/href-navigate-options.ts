import type { AnyRouter, NavigateOptions } from "@tanstack/react-router"

/**
 * The router options for a plain React Aria `href`, such as on a `MenuItem`.
 * The router goes to an address that has a route. An address with no route,
 * such as `/bff/login`, loads the page from the server.
 */
export function hrefNavigateOptions(router: AnyRouter, href: string): NavigateOptions {
  const url = new URL(href, window.location.href)
  return router.getMatchedRoutes(url.pathname).foundRoute
    ? { href }
    : { href: url.href, reloadDocument: true }
}

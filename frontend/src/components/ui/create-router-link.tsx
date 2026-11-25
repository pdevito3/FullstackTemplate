import type * as React from "react"
import { createLink, type LinkComponent } from "@tanstack/react-router"

import type { RouterLinkHostProps } from "@/components/ui/link"

/**
 * Makes a host that renders `RouterLinkPrimitive` a TanStack Router link with
 * typed `to`, `params`, and `search`. The link sets `aria-current="page"` only
 * when it goes to the current page, not to a parent of the current page.
 */
export function createRouterLink<
  THost extends React.ComponentType<RouterLinkHostProps>,
>(Host: THost): LinkComponent<THost> {
  // The returned type checks the props of each caller. The props go on to
  // createLink as they are, with the router options added. The host type has
  // no `routerOptions`, so they go in with the spread.
  const RouterLink = createLink(Host)

  return (props) => {
    const hostProps = { ...props, routerOptions: props }

    return <RouterLink activeOptions={{ exact: true }} {...hostProps} />
  }
}

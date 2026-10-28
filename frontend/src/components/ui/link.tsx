import type * as React from "react"
import { useRouter, type NavigateOptions } from "@tanstack/react-router"
import {
  Link as LinkPrimitive,
  RouterProvider,
  type LinkProps as LinkPrimitiveProps,
} from "react-aria-components"

import { createRouterLink } from "@/components/ui/create-router-link"
import { hrefNavigateOptions } from "@/components/ui/href-navigate-options"

declare module "react-aria-components" {
  interface RouterConfig {
    routerOptions: NavigateOptions
  }
}

/**
 * The props that `createRouterLink` gives to its host. The router builds the
 * `href` and the `routerOptions`, so a caller gives `to` and `params`.
 * `className` and `style` are not render props, because the router joins the
 * className with the class of the active state. Use `onPress`, not `onClick`.
 */
export type RouterLinkHostProps = Omit<
  LinkPrimitiveProps,
  "href" | "routerOptions" | "className" | "style" | "onClick"
> &
  React.RefAttributes<HTMLAnchorElement> & {
    className?: string
    style?: React.CSSProperties
    /** The `disabled` option of the router link. */
    disabled?: boolean
  }

/**
 * The React Aria Link to render in a host of `createRouterLink`.
 *
 * React Aria decides for each press (pointer, keyboard, or screen reader)
 * whether the router navigates or the browser opens the link, for example in
 * a new tab. The click handler of TanStack Router is dropped: React Aria calls
 * it after a key press only on key up, when the browser has already loaded the
 * page again.
 */
export function RouterLinkPrimitive({
  disabled,
  isDisabled,
  ...props
}: RouterLinkHostProps) {
  const router = useRouter()
  return (
    <RouterProvider navigate={(href, options) => router.navigate(options ?? { href })}>
      <LinkPrimitive
        {...props}
        isDisabled={isDisabled || disabled || undefined}
        onClick={undefined}
      />
    </RouterProvider>
  )
}

export const Link = createRouterLink(RouterLinkPrimitive)

interface HrefRouterProviderProps {
  children: React.ReactNode
}

/** Sends a plain React Aria `href` in the app to the router. */
export function HrefRouterProvider({ children }: HrefRouterProviderProps) {
  const router = useRouter()
  return (
    <RouterProvider
      navigate={(href, options) => router.navigate(options ?? hrefNavigateOptions(router, href))}
    >
      {children}
    </RouterProvider>
  )
}

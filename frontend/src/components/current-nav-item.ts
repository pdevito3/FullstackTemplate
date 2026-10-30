import type { SidebarLinkCurrent } from "@/components/ui/sidebar"

/** Tells if the router matches the current location to `url`. */
export type MatchNavUrl<TUrl extends string> = (url: TUrl, fuzzy: boolean) => boolean

export interface CurrentNavItem<TUrl extends string = string> {
  url: TUrl
  /** The `aria-current` value of the item. */
  current: SidebarLinkCurrent
}

/** Finds the one item of the navigation that the current location belongs to. */
export function currentNavItem<TUrl extends string>(
  urls: readonly TUrl[],
  matchUrl: MatchNavUrl<TUrl>,
): CurrentNavItem<TUrl> | undefined {
  const page = urls.find((url) => matchUrl(url, false))
  return page ? { url: page, current: "page" } : undefined
}

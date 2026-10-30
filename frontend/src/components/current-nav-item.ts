import type { SidebarLinkCurrent } from "@/components/ui/sidebar"

/**
 * Tells if the router matches the current location to `url`. A fuzzy match
 * also matches a page under `url`, by whole path segments.
 */
export type MatchNavUrl<TUrl extends string> = (url: TUrl, fuzzy: boolean) => boolean

export interface CurrentNavItem<TUrl extends string = string> {
  url: TUrl
  /**
   * The `aria-current` value of the item: `page` on its own page, and `true`
   * on a page under it, because then the link does not go to the current page.
   */
  current: SidebarLinkCurrent
}

const segmentCount = (url: string) => url.split("/").filter(Boolean).length

/**
 * Finds the one item of the navigation that the current location belongs to:
 * the item of the current page, or else the most specific item above it.
 */
export function currentNavItem<TUrl extends string>(
  urls: readonly TUrl[],
  matchUrl: MatchNavUrl<TUrl>,
): CurrentNavItem<TUrl> | undefined {
  const page = urls.find((url) => matchUrl(url, false))
  if (page) return { url: page, current: "page" }

  const section = urls
    .filter((url) => matchUrl(url, true))
    .reduce<TUrl | undefined>((best, url) => (best && segmentCount(best) >= segmentCount(url) ? best : url), undefined)
  return section ? { url: section, current: "true" } : undefined
}

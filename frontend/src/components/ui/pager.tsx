import type * as React from "react"

import type { PaginationMetadata } from "@/api/generated/models/types/pagination-metadata"
import { Button } from "@/components/ui/button"

interface PagerButtonProps {
  isAvailable: boolean
  onPress: () => void
  children: React.ReactNode
}

/**
 * A pager button that keeps the focus at the first and the last page. A disabled button loses the focus to the
 * body, so a keyboard user who pressed Next to reach the last page would have to Tab from the start of the page.
 */
function PagerButton({ isAvailable, onPress, children }: PagerButtonProps) {
  return (
    <Button
      variant="outline"
      size="sm"
      aria-disabled={!isAvailable || undefined}
      className="aria-disabled:pointer-events-none aria-disabled:opacity-50 aria-disabled:focus-visible:ring-ring"
      onPress={() => {
        if (isAvailable) onPress()
      }}
    >
      {children}
    </Button>
  )
}

interface PagerProps {
  /** The accessible name of the pager, for example `Pages of users`. */
  label: string
  /** The page data of the last answer of a list endpoint, from `parsePagination`. */
  pagination: Pick<PaginationMetadata, "pageNumber" | "totalPages" | "hasPrevious" | "hasNext">
  onPageChange: (pageNumber: number) => void
}

/** Previous and Next for a list that the Server pages. The Server decides if there is a page before or after. */
function Pager({ label, pagination, onPageChange }: PagerProps) {
  const { pageNumber, totalPages, hasPrevious, hasNext } = pagination

  return (
    <nav aria-label={label} className="flex items-center justify-end gap-2">
      <span className="mr-auto text-sm text-muted-foreground">
        Page {pageNumber} of {Math.max(totalPages, 1)}
      </span>
      <PagerButton isAvailable={hasPrevious} onPress={() => onPageChange(pageNumber - 1)}>
        Previous
      </PagerButton>
      <PagerButton isAvailable={hasNext} onPress={() => onPageChange(pageNumber + 1)}>
        Next
      </PagerButton>
    </nav>
  )
}

export { Pager, PagerButton }

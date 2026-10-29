import { useEffect, useRef, type ReactNode, type Ref } from "react"

import type { PaginationMetadata } from "@/api/generated/models/types/pagination-metadata"
import { Button } from "@/components/ui/button"

interface PagerButtonProps {
  isAvailable: boolean
  onPress: () => void
  ref: Ref<HTMLButtonElement>
  children: ReactNode
}

function PagerButton({ isAvailable, onPress, ref, children }: PagerButtonProps) {
  return (
    <Button
      ref={ref}
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

interface PagerButtonsProps {
  hasPrevious: boolean
  hasNext: boolean
  onPrevious: () => void
  onNext: () => void
}

/**
 * Previous and Next. A press that reaches the first or the last page makes the pressed button do nothing, so the
 * focus moves to the other button. The buttons use `aria-disabled`, not `disabled`, because a disabled button drops
 * the focus to the body before the move.
 */
function PagerButtons({ hasPrevious, hasNext, onPrevious, onNext }: PagerButtonsProps) {
  const previous = useRef<HTMLButtonElement>(null)
  const next = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    const focused = document.activeElement
    if (!hasNext && focused === next.current) previous.current?.focus()
    else if (!hasPrevious && focused === previous.current) next.current?.focus()
  }, [hasPrevious, hasNext])

  return (
    <>
      <PagerButton ref={previous} isAvailable={hasPrevious} onPress={onPrevious}>
        Previous
      </PagerButton>
      <PagerButton ref={next} isAvailable={hasNext} onPress={onNext}>
        Next
      </PagerButton>
    </>
  )
}

interface PagerProps {
  label: string
  pagination: Pick<PaginationMetadata, "pageNumber" | "totalPages" | "hasPrevious" | "hasNext">
  onPageChange: (pageNumber: number) => void
}

function Pager({ label, pagination, onPageChange }: PagerProps) {
  const { pageNumber, totalPages, hasPrevious, hasNext } = pagination

  return (
    <nav aria-label={label} className="flex items-center justify-end gap-2">
      <span className="mr-auto text-sm text-muted-foreground">
        Page {pageNumber} of {Math.max(totalPages, 1)}
      </span>
      <PagerButtons
        hasPrevious={hasPrevious}
        hasNext={hasNext}
        onPrevious={() => onPageChange(pageNumber - 1)}
        onNext={() => onPageChange(pageNumber + 1)}
      />
    </nav>
  )
}

export { Pager, PagerButtons }

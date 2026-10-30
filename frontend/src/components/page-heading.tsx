import type { ReactNode, RefObject } from 'react'

import { cn } from '@/lib/utils'

interface PageHeadingProps {
  children: ReactNode
  className?: string
  ref?: RefObject<HTMLHeadingElement | null>
}

// The root layout moves the focus to the heading after a navigation. tabIndex lets a page move it there after an action.
// A window narrower than 20rem is a 320 px phone with a font larger than 16 px. There the heading is one size smaller, so
// that a word of the heading fits the width and the page does not scroll sideways.
export function PageHeading({ children, className, ref }: PageHeadingProps) {
  return (
    <h1
      ref={ref}
      tabIndex={-1}
      className={cn('text-2xl font-bold tracking-tight outline-none min-[20rem]:text-3xl', className)}
    >
      {children}
    </h1>
  )
}

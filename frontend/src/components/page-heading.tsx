import type { ReactNode, RefObject } from 'react'

import { cn } from '@/lib/utils'

interface PageHeadingProps {
  children: ReactNode
  className?: string
  ref?: RefObject<HTMLHeadingElement | null>
}

// The root layout moves the focus to the heading after a navigation. tabIndex lets a page move it there after an action.
// A media query measures rem in the font size of the browser, so a window narrower than 20rem is narrow for its text:
// under 320 px at a 16 px font, under 480 px at a 24 px font. There the heading is one size smaller, so that a word of it fits.
export function PageHeading({ children, className, ref }: PageHeadingProps) {
  return (
    <h1
      ref={ref}
      tabIndex={-1}
      className={cn('text-2xl font-bold tracking-tight wrap-anywhere outline-none min-[20rem]:text-3xl', className)}
    >
      {children}
    </h1>
  )
}
